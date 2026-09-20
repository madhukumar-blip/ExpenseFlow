using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Application.Expenses;

public enum ReviewDecision
{
    Approve = 1,
    Reject = 2
}

public sealed record PendingExpenseItem(
    Guid Id,
    string EmployeeEmail,
    string Title,
    string Description,
    ExpenseCategory Category,
    decimal Amount,
    DateOnly ExpenseDate,
    byte[] RowVersion);