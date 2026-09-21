using ExpenseFlow.Application.Expenses;

namespace ExpenseFlow.Infrastructure.Storage;

public sealed class LocalReceiptStorage : IReceiptStorage
{
    private const long MaximumFileSize = 5 * 1024 * 1024;

    private readonly string _storagePath;

    public LocalReceiptStorage(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new ArgumentException(
                "Receipt storage path is required.",
                nameof(storagePath));
        }

        _storagePath = Path.GetFullPath(storagePath);
        Directory.CreateDirectory(_storagePath);
    }

    public async Task<StoredReceiptFile> SaveAsync(
        Stream content,
        string originalFileName,
        long declaredLength,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            throw new ArgumentException(
                "Select a receipt file.",
                nameof(originalFileName));
        }

        if (declaredLength <= 0 ||
            declaredLength > MaximumFileSize)
        {
            throw new ArgumentException(
                "The receipt must be between 1 byte and 5 MB.",
                nameof(declaredLength));
        }

        var safeOriginalFileName =
            Path.GetFileName(originalFileName.Trim());

        if (safeOriginalFileName.Length == 0 ||
            safeOriginalFileName.Length > 255)
        {
            throw new ArgumentException(
                "The receipt filename is invalid.",
                nameof(originalFileName));
        }

        var extension = Path
            .GetExtension(safeOriginalFileName)
            .ToLowerInvariant();

        if (extension is not ".pdf" and
            not ".jpg" and
            not ".jpeg" and
            not ".png")
        {
            throw new ArgumentException(
                "Only PDF, JPG, JPEG, and PNG receipts are allowed.",
                nameof(originalFileName));
        }

        var temporaryFileName =
            $"{Guid.NewGuid():N}.upload";

        var temporaryPath = Path.Combine(
            _storagePath,
            temporaryFileName);

        try
        {
            var actualLength = await CopyWithLimitAsync(
                content,
                temporaryPath,
                cancellationToken);

            if (actualLength == 0)
            {
                throw new ArgumentException(
                    "The selected receipt is empty.",
                    nameof(content));
            }

            var contentType = await ValidateSignatureAsync(
                temporaryPath,
                extension,
                cancellationToken);

            var storedFileName =
                $"{Guid.NewGuid():N}{extension}";

            var finalPath = Path.Combine(
                _storagePath,
                storedFileName);

            File.Move(temporaryPath, finalPath);

            return new StoredReceiptFile(
                storedFileName,
                safeOriginalFileName,
                contentType,
                actualLength);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(
        string storedFileName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = ResolveStoredPath(storedFileName);

        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true);

        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteAsync(
        string storedFileName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var path = ResolveStoredPath(storedFileName);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private async Task<long> CopyWithLimitAsync(
        Stream source,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var destination = new FileStream(
            destinationPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);

        var buffer = new byte[64 * 1024];
        long totalBytes = 0;

        while (true)
        {
            var bytesRead = await source.ReadAsync(
                buffer.AsMemory(),
                cancellationToken);

            if (bytesRead == 0)
            {
                break;
            }

            totalBytes += bytesRead;

            if (totalBytes > MaximumFileSize)
            {
                throw new ArgumentException(
                    "The receipt cannot exceed 5 MB.");
            }

            await destination.WriteAsync(
                buffer.AsMemory(0, bytesRead),
                cancellationToken);
        }

        return totalBytes;
    }

    private static async Task<string> ValidateSignatureAsync(
        string path,
        string extension,
        CancellationToken cancellationToken)
    {
        var header = new byte[8];

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        var bytesRead = await stream.ReadAsync(
            header.AsMemory(),
            cancellationToken);

        var valid = extension switch
        {
            ".pdf" => bytesRead >= 5 &&
                header[0] == 0x25 &&
                header[1] == 0x50 &&
                header[2] == 0x44 &&
                header[3] == 0x46 &&
                header[4] == 0x2D,

            ".jpg" or ".jpeg" => bytesRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF,

            ".png" => bytesRead >= 8 &&
                header.SequenceEqual(new byte[]
                {
                    0x89, 0x50, 0x4E, 0x47,
                    0x0D, 0x0A, 0x1A, 0x0A
                }),

            _ => false
        };

        if (!valid)
        {
            throw new ArgumentException(
                "The file content does not match its extension.");
        }

        return extension switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            _ => "image/jpeg"
        };
    }

    private string ResolveStoredPath(string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
        {
            throw new ArgumentException(
                "Stored filename is required.",
                nameof(storedFileName));
        }

        var fileName = Path.GetFileName(storedFileName);

        if (!string.Equals(
                fileName,
                storedFileName,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Stored filename is invalid.",
                nameof(storedFileName));
        }

        var fullPath = Path.GetFullPath(
            Path.Combine(_storagePath, fileName));

        var requiredPrefix =
            _storagePath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(
                requiredPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Receipt path is outside the storage directory.");
        }

        return fullPath;
    }
}