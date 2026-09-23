namespace ExpenseFlow.Application.Expenses;

public sealed class ExpenseReviewService
{
    private readonly IExpenseStore _store;
    private readonly IReceiptStorage _receiptStorage;

    public ExpenseReviewService(
     IExpenseStore store,
     IReceiptStorage receiptStorage)
    {
        _store = store;
        _receiptStorage = receiptStorage;
    }

    public async Task<ReceiptDownload?> GetReceiptAsync(
    Guid expenseId,
    string managerId,
    CancellationToken cancellationToken)
    {
        ValidateManagerId(managerId);

        if (expenseId == Guid.Empty)
        {
            return null;
        }

        var expense = await _store.FindForManagerReceiptAsync(
            expenseId,
            managerId,
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

        return stream is null
            ? null
            : new ReceiptDownload(
                stream,
                expense.ReceiptOriginalFileName,
                expense.ReceiptContentType);
    }

    public Task<IReadOnlyList<PendingExpenseItem>> ListPendingAsync(
        string managerId,
        CancellationToken cancellationToken)
    {
        ValidateManagerId(managerId);

        return _store.ListPendingAsync(
            managerId,
            cancellationToken);
    }

    public async Task<bool> ReviewAsync(
        Guid expenseId,
        string managerId,
        ReviewDecision decision,
        string? reason,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        ValidateManagerId(managerId);

        if (!Enum.IsDefined(typeof(ReviewDecision), decision))
        {
            throw new ArgumentException("Invalid review decision.");
        }

        if (expectedRowVersion is null ||
            expectedRowVersion.Length != 8)
        {
            throw new ArgumentException("Invalid expense version.");
        }

        var expense = await _store.FindForReviewAsync(
            expenseId,
            managerId,
            cancellationToken);

        if (expense is null)
        {
            return false;
        }

        if (!expense.RowVersion.SequenceEqual(expectedRowVersion))
        {
            throw new ExpenseConflictException(
                "This expense has changed. Review the refreshed queue.");
        }

        if (decision == ReviewDecision.Approve)
        {
            expense.Approve(managerId);
        }
        else
        {
            expense.Reject(managerId, reason);
        }

        await _store.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void ValidateManagerId(string managerId)
    {
        if (string.IsNullOrWhiteSpace(managerId))
        {
            throw new ArgumentException("Manager ID is required.");
        }
    }
}