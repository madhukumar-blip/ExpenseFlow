using System.ComponentModel.DataAnnotations;
using ExpenseFlow.Application.Expenses;

namespace ExpenseFlow.Web.Models.Expenses;

public sealed class ReviewExpenseViewModel
{
    public Guid Id { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    [EnumDataType(typeof(ReviewDecision))]
    public ReviewDecision Decision { get; set; }

    [Display(Name = "Manager comment")]
    [StringLength(1000)]
    public string? Comment { get; set; }
}