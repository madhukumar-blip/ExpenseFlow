using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Application.Expenses;

public sealed class ExpenseReportingService
{
    private readonly IExpenseStore _store;

    public ExpenseReportingService(IExpenseStore store)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<ExpenseStatusSummary>> GetSummaryAsync(
        string employeeId,
        CancellationToken cancellationToken)
    {
        ValidateUser(employeeId);

        var results = await _store.GetSummaryAsync(
            employeeId,
            cancellationToken);

        return Enum.GetValues<ExpenseStatus>()
            .Select(status =>
                results.FirstOrDefault(item => item.Status == status)
                ?? new ExpenseStatusSummary(status, 0, 0m))
            .ToList();
    }

    public Task<ExpenseDetails?> GetDetailsAsync(
        Guid expenseId,
        string employeeId,
        CancellationToken cancellationToken)
    {
        ValidateUser(employeeId);

        return _store.GetOwnedDetailsAsync(
            expenseId,
            employeeId,
            cancellationToken);
    }

    private static void ValidateUser(string employeeId)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            throw new ArgumentException("Employee ID is required.");
        }
    }
}