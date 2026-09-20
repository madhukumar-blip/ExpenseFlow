using System.ComponentModel.DataAnnotations;

namespace ExpenseFlow.Web.Models.Expenses;

public sealed class ReimburseExpenseViewModel
{
    public Guid Id { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string PaymentReference { get; set; } = string.Empty;
}