using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Application.Expenses;

public sealed record CreateExpenseCommand(
    string Title,
    string Description,
    decimal Amount,
    DateOnly ExpenseDate,
    ExpenseCategory Category);

public sealed record ExpenseListItem(
    Guid Id,
    string Title,
    ExpenseCategory Category,
    decimal Amount,
    DateOnly ExpenseDate,
    ExpenseStatus Status,
    byte[] RowVersion,
    string? RejectionReason);

public interface IExpenseStore
{
    Task<PagedExpenses> SearchAsync(
    string employeeId,
    ExpenseSearch filter,
    CancellationToken cancellationToken);

    Task<IReadOnlyList<FinanceExpenseItem>> ListApprovedAsync(
    string financeUserId,
    CancellationToken cancellationToken);

    Task<Expense?> FindForReimbursementAsync(
        Guid expenseId,
        string financeUserId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ExpenseStatusSummary>> GetSummaryAsync(
        string employeeId,
        CancellationToken cancellationToken);

    Task<ExpenseDetails?> GetOwnedDetailsAsync(
        Guid expenseId,
        string employeeId,
        CancellationToken cancellationToken);

    Task AddAsync(
        Expense expense,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ExpenseListItem>> ListForEmployeeAsync(
        string employeeId,
        CancellationToken cancellationToken);

    Task<Expense?> FindOwnedAsync(
        Guid expenseId,
        string employeeId,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PendingExpenseItem>> ListPendingAsync(
    string managerId,
    CancellationToken cancellationToken);

    Task<Expense?> FindForReviewAsync(
        Guid expenseId,
        string managerId,
        CancellationToken cancellationToken);
}