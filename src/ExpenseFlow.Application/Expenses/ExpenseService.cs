using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Application.Expenses;

public sealed class ExpenseService
{
    private readonly IExpenseStore _store;
    private readonly IReceiptStorage _receiptStorage;

    public ExpenseService(IExpenseStore store, IReceiptStorage receiptStorage)
    {
        _store = store;
        _receiptStorage = receiptStorage;
    }

    public async Task<bool> RemoveReceiptAsync(Guid expenseId, string employeeId, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        ValidateEmployeeId(employeeId);
        ValidateRowVersion(expectedRowVersion);

        if (expenseId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid expense ID is required.",
                nameof(expenseId));
        }

        var expense = await _store.FindOwnedAsync(
            expenseId,
            employeeId,
            cancellationToken);

        if (expense is null)
        {
            return false;
        }

        if (expense.Status != ExpenseStatus.Draft)
        {
            throw new InvalidOperationException(
                "Receipts can only be removed from draft expenses.");
        }

        if (!expense.RowVersion.SequenceEqual(expectedRowVersion))
        {
            throw new ExpenseConflictException(
                "This expense was changed. Reload and try again.");
        }

        if (string.IsNullOrWhiteSpace(
            expense.ReceiptStoredFileName))
        {
            throw new InvalidOperationException(
                "This expense does not have a receipt.");
        }

        var originalFileName =
    expense.ReceiptOriginalFileName;

        var storedFileName =
    expense.ReceiptStoredFileName;

        expense.RemoveReceipt();


        _store.AddAudit(new ExpenseAuditEntry(
            expense.Id,
            employeeId,
            ExpenseAuditAction.ReceiptRemoved,
            previousStatus: ExpenseStatus.Draft,
            newStatus: ExpenseStatus.Draft,
            comment: originalFileName));

        await _store.SaveChangesAsync(cancellationToken);

        await TryDeleteStoredReceiptAsync(
            storedFileName,
            cancellationToken);

        return true;
    }

    public async Task<ReceiptDownload?> DownloadReceiptAsync(Guid expenseId, string employeeId, CancellationToken cancellationToken)
    {
        ValidateEmployeeId(employeeId);

        if (expenseId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid expense ID is required.",
                nameof(expenseId));
        }

        var expense = await _store.FindOwnedAsync(
            expenseId,
            employeeId,
            cancellationToken);

        if (expense is null ||
            string.IsNullOrWhiteSpace(expense.ReceiptStoredFileName) ||
            string.IsNullOrWhiteSpace(expense.ReceiptOriginalFileName) ||
            string.IsNullOrWhiteSpace(expense.ReceiptContentType))
        {
            return null;
        }

        var stream = await _receiptStorage.OpenReadAsync(
            expense.ReceiptStoredFileName,
            cancellationToken);

        if (stream is null)
        {
            return null;
        }

        return new ReceiptDownload(
            stream,
            expense.ReceiptOriginalFileName,
            expense.ReceiptContentType);
    }

    public async Task<bool> UploadReceiptAsync(Guid expenseId, string employeeId, ReceiptUploadCommand command, CancellationToken cancellationToken)
    {
        ValidateEmployeeId(employeeId);
        ValidateRowVersion(command.RowVersion);

        if (expenseId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid expense ID is required.",
                nameof(expenseId));
        }

        ArgumentNullException.ThrowIfNull(command.Content);

        var expense = await _store.FindOwnedAsync(
            expenseId,
            employeeId,
            cancellationToken);

        if (expense is null)
        {
            return false;
        }

        if (expense.Status != ExpenseStatus.Draft)
        {
            throw new InvalidOperationException(
                "Receipts can only be added to draft expenses.");
        }

        if (!expense.RowVersion.SequenceEqual(command.RowVersion))
        {
            throw new ExpenseConflictException(
                "This expense was changed. Reload and try again.");
        }

        var previousStoredFileName =
            expense.ReceiptStoredFileName;

        var storedFile = await _receiptStorage.SaveAsync(
            command.Content,
            command.OriginalFileName,
            command.Length,
            cancellationToken);

        try
        {
            expense.AttachReceipt(
                storedFile.StoredFileName,
                storedFile.OriginalFileName,
                storedFile.ContentType,
                storedFile.Size);

            var auditAction =
    string.IsNullOrWhiteSpace(previousStoredFileName)
        ? ExpenseAuditAction.ReceiptUploaded
        : ExpenseAuditAction.ReceiptReplaced;

            _store.AddAudit(new ExpenseAuditEntry(
    expense.Id,
    employeeId,
    ExpenseAuditAction.Deleted,
    previousStatus: ExpenseStatus.Draft,
    newStatus: null,
    comment: $"Draft expense deleted: {expense.Title}"));

            _store.AddAudit(new ExpenseAuditEntry(
                expense.Id,
                employeeId,
                auditAction,
                previousStatus: ExpenseStatus.Draft,
                newStatus: ExpenseStatus.Draft,
                comment: storedFile.OriginalFileName));

            await _store.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _receiptStorage.DeleteAsync(
                storedFile.StoredFileName,
                CancellationToken.None);

            throw;
        }

        if (!string.IsNullOrWhiteSpace(previousStoredFileName))
        {
            await TryDeleteStoredReceiptAsync(
                previousStoredFileName,
                cancellationToken);
        }

        return true;
    }

    public Task<PagedExpenses> SearchAsync(string employeeId, ExpenseSearch filter, CancellationToken cancellationToken)
    {
        ValidateEmployeeId(employeeId);

        if (filter.Search?.Length > 100)
        {
            throw new ArgumentException(
                "Search text cannot exceed 100 characters.");
        }

        if (filter.Status.HasValue &&
            !Enum.IsDefined(typeof(ExpenseStatus), filter.Status.Value))
        {
            throw new ArgumentException("Select a valid status.");
        }

        if (filter.Category.HasValue &&
            !Enum.IsDefined(typeof(ExpenseCategory), filter.Category.Value))
        {
            throw new ArgumentException("Select a valid category.");
        }

        if (filter.From.HasValue &&
            filter.To.HasValue &&
            filter.From.Value > filter.To.Value)
        {
            throw new ArgumentException(
                "From date must be on or before To date.");
        }

        return _store.SearchAsync(
            employeeId,
            filter,
            cancellationToken);
    }

    public async Task<Guid> CreateAsync(string employeeId, CreateExpenseCommand command, CancellationToken cancellationToken)
    {
        var expense = new Expense(
            employeeId,
            command.Title,
            command.Description,
            command.Amount,
            command.ExpenseDate);

        expense.ChangeCategory(command.Category);

        await _store.AddAsync(expense, cancellationToken);

        _store.AddAudit(new ExpenseAuditEntry(
            expense.Id,
            employeeId,
            ExpenseAuditAction.Created,
            previousStatus: null,
            newStatus: ExpenseStatus.Draft,
            comment: "Expense created as a draft."));

        await _store.SaveChangesAsync(cancellationToken);

        return expense.Id;
    }

    public Task<IReadOnlyList<ExpenseListItem>> ListAsync(string employeeId, CancellationToken cancellationToken)
    {
        ValidateEmployeeId(employeeId);

        return _store.ListForEmployeeAsync(
            employeeId,
            cancellationToken);
    }

    public Task<ExpenseDraft?> GetDraftAsync(Guid expenseId, string employeeId, CancellationToken cancellationToken)
    {
        ValidateEmployeeId(employeeId);

        if (expenseId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid expense ID is required.",
                nameof(expenseId));
        }

        return _store.GetOwnedDraftAsync(
            expenseId,
            employeeId,
            cancellationToken);
    }

    public async Task<bool> UpdateAsync(Guid expenseId, string employeeId, UpdateExpenseCommand command, CancellationToken cancellationToken)
    {
        ValidateEmployeeId(employeeId);
        ValidateRowVersion(command.RowVersion);

        if (expenseId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid expense ID is required.",
                nameof(expenseId));
        }

        var expense = await _store.FindOwnedAsync(
            expenseId,
            employeeId,
            cancellationToken);

        if (expense is null)
        {
            return false;
        }

        if (!expense.RowVersion.SequenceEqual(command.RowVersion))
        {
            throw new ExpenseConflictException(
                "This expense was changed by another operation. "
                + "Reload the page and try again.");
        }

        expense.UpdateDraft(
            command.Title,
            command.Description,
            command.Amount,
            command.ExpenseDate);

        expense.ChangeCategory(command.Category);

        _store.AddAudit(new ExpenseAuditEntry(
    expense.Id,
    employeeId,
    ExpenseAuditAction.Updated,
    previousStatus: ExpenseStatus.Draft,
    newStatus: ExpenseStatus.Draft,
    comment: "Draft expense details updated."));

        await _store.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<bool> DeleteAsync(Guid expenseId, string employeeId, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        ValidateEmployeeId(employeeId);
        ValidateRowVersion(expectedRowVersion);

        if (expenseId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid expense ID is required.",
                nameof(expenseId));
        }

        var expense = await _store.FindOwnedAsync(
            expenseId,
            employeeId,
            cancellationToken);

        if (expense is null)
        {
            return false;
        }

        if (!expense.RowVersion.SequenceEqual(expectedRowVersion))
        {
            throw new ExpenseConflictException(
                "This expense was changed by another operation. "
                + "Reload the page and try again.");
        }

        if (expense.Status != ExpenseStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only draft expenses can be deleted.");
        }

        var storedFileName =
      expense.ReceiptStoredFileName;

        _store.Remove(expense);

        await _store.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(storedFileName))
        {
            await TryDeleteStoredReceiptAsync(
                storedFileName,
                cancellationToken);
        }

        return true;
    }

    public async Task<bool> SubmitAsync(Guid expenseId, string employeeId, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        ValidateEmployeeId(employeeId);

        ValidateRowVersion(expectedRowVersion);

        var expense = await _store.FindOwnedAsync(
            expenseId,
            employeeId,
            cancellationToken);

        if (expense is null)
        {
            return false;
        }

        if (!expense.RowVersion.SequenceEqual(expectedRowVersion))
        {
            throw new ExpenseConflictException(
                "This expense has changed. Review the refreshed list.");
        }

        expense.Submit();

        _store.AddAudit(new ExpenseAuditEntry(
    expense.Id,
    employeeId,
    ExpenseAuditAction.Submitted,
    previousStatus: ExpenseStatus.Draft,
    newStatus: ExpenseStatus.Submitted,
    comment: "Submitted for manager approval."));

        await _store.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void ValidateEmployeeId(string employeeId)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            throw new ArgumentException(
                "Employee ID is required.",
                nameof(employeeId));
        }
    }

    private static void ValidateRowVersion(byte[]? rowVersion)
    {
        if (rowVersion is null || rowVersion.Length != 8)
        {
            throw new ArgumentException(
                "A valid expense version is required.",
                nameof(rowVersion));
        }
    }

    private async Task TryDeleteStoredReceiptAsync(string storedFileName, CancellationToken cancellationToken)
    {
        try
        {
            await _receiptStorage.DeleteAsync(
                storedFileName,
                cancellationToken);
        }
        catch (IOException)
        {

        }
        catch (UnauthorizedAccessException)
        {

        }
    }
}