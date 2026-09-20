using ExpenseFlow.Application.Expenses;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Web.Models.Expenses;

public sealed class ExpenseIndexViewModel
{
    public string? Search { get; set; }

    public ExpenseStatus? Status { get; set; }

    public ExpenseCategory? Category { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    public PagedExpenses Results { get; set; } =
        new(Array.Empty<ExpenseListItem>(), 0, 1, 10);
}