using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Application.Expenses;

public sealed record FinanceExpenseItem(
    Guid Id,
    string EmployeeEmail,
    string Title,
    decimal Amount,
    DateTimeOffset? ApprovedAtUtc,
    byte[] RowVersion);

public sealed record ExpenseStatusSummary(
    ExpenseStatus Status,
    int Count,
    decimal TotalAmount);

public sealed record ExpenseHistoryEntry(
    string Action,
    DateTimeOffset OccurredAtUtc,
    string Actor,
    string? Note);

public sealed record ExpenseDetails(
    Guid Id,
    string Title,
    string Description,
    ExpenseCategory Category,
    decimal Amount,
    DateOnly ExpenseDate,
    ExpenseStatus Status,
    string? ReceiptOriginalFileName,
    string? ReceiptContentType,
    long? ReceiptSize,
    byte[] RowVersion,
    IReadOnlyList<ExpenseHistoryEntry> History);