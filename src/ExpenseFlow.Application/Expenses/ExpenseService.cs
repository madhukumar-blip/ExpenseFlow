using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Application.Expenses;

public sealed class ExpenseService
{
    private readonly IExpenseStore _store;

    public ExpenseService(IExpenseStore store)
    {
        _store = store;
    }

    public Task<PagedExpenses> SearchAsync(    string employeeId,    ExpenseSearch filter,    CancellationToken cancellationToken)
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

    public async Task<Guid> CreateAsync(
        string employeeId,
        CreateExpenseCommand command,
        CancellationToken cancellationToken)
    {
        var expense = new Expense(
            employeeId,
            command.Title,
            command.Description,
            command.Amount,
            command.ExpenseDate);

        expense.ChangeCategory(command.Category);

        await _store.AddAsync(expense, cancellationToken);

        return expense.Id;
    }

    public Task<IReadOnlyList<ExpenseListItem>> ListAsync(
        string employeeId,
        CancellationToken cancellationToken)
    {
        ValidateEmployeeId(employeeId);

        return _store.ListForEmployeeAsync(
            employeeId,
            cancellationToken);
    }

    public async Task<bool> SubmitAsync(
        Guid expenseId,
        string employeeId,
        byte[] expectedRowVersion,
        CancellationToken cancellationToken)
    {
        ValidateEmployeeId(employeeId);

        if (expectedRowVersion is null ||
            expectedRowVersion.Length != 8)
        {
            throw new ArgumentException(
                "A valid expense version is required.",
                nameof(expectedRowVersion));
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
                "This expense has changed. Review the refreshed list.");
        }

        expense.Submit();

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
}