namespace ExpenseFlow.Application.Expenses;

public sealed class ExpenseFinanceService
{
    private readonly IExpenseStore _store;

    public ExpenseFinanceService(IExpenseStore store)
    {
        _store = store;
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