namespace ExpenseFlow.Application.Expenses;

public sealed class ExpenseFinanceService
{
    private readonly IExpenseStore _store;
    private readonly IReceiptStorage _receiptStorage;

    public ExpenseFinanceService(
     IExpenseStore store,
     IReceiptStorage receiptStorage)
    {
        _store = store;
        _receiptStorage = receiptStorage;
    }

    public async Task<ReceiptDownload?> GetReceiptAsync(
    Guid expenseId,
    string financeUserId,
    CancellationToken cancellationToken)
    {
        ValidateUser(financeUserId);

        if (expenseId == Guid.Empty)
        {
            return null;
        }

        var expense = await _store.FindForFinanceReceiptAsync(
            expenseId,
            financeUserId,
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

    public Task<IReadOnlyList<FinanceExpenseItem>> ListApprovedAsync(
        string financeUserId,
        CancellationToken cancellationToken)
    {
        ValidateUser(financeUserId);

        return _store.ListApprovedAsync(
            financeUserId,
            cancellationToken);
    }

    public async Task<bool> ReimburseAsync(
        Guid expenseId,
        string financeUserId,
        string paymentReference,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        ValidateUser(financeUserId);

        if (expectedRowVersion is null ||
            expectedRowVersion.Length != 8)
        {
            throw new ArgumentException("Invalid expense version.");
        }

        var expense = await _store.FindForReimbursementAsync(
            expenseId,
            financeUserId,
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

        expense.Reimburse(financeUserId, paymentReference);

        await _store.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void ValidateUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("Finance user ID is required.");
        }
    }
}