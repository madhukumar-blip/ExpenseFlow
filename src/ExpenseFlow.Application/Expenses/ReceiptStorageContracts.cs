namespace ExpenseFlow.Application.Expenses;

public sealed record StoredReceiptFile(
    string StoredFileName,
    string OriginalFileName,
    string ContentType,
    long Size);

public sealed record ReceiptUploadCommand(
    Stream Content,
    string OriginalFileName,
    long Length,
    byte[] RowVersion);

public sealed record ReceiptDownload(
    Stream Content,
    string OriginalFileName,
    string ContentType);

public interface IReceiptStorage
{
    Task<StoredReceiptFile> SaveAsync(
        Stream content,
        string originalFileName,
        long declaredLength,
        CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(
        string storedFileName,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string storedFileName,
        CancellationToken cancellationToken);
}