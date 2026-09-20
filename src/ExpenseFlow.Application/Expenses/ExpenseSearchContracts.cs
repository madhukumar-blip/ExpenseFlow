using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Application.Expenses;

public sealed record ExpenseSearch(
    string? Search,
    ExpenseStatus? Status,
    ExpenseCategory? Category,
    DateOnly? From,
    DateOnly? To,
    int PageNumber = 1);

public sealed record PagedExpenses(
    IReadOnlyList<ExpenseListItem> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    public int TotalPages =>
        Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));

    public bool HasPrevious => PageNumber > 1;

    public bool HasNext => PageNumber < TotalPages;
}