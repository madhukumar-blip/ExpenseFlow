using System.ComponentModel.DataAnnotations;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Web.Models.Expenses;

public sealed class CreateExpenseViewModel : IValidatableObject
{
    [Required]
    [StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Amount (INR)")]
    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal? Amount { get; set; }

    [Required]
    [Display(Name = "Expense date")]
    [DataType(DataType.Date)]
    public DateOnly? ExpenseDate { get; set; }

    [Required]
    [EnumDataType(typeof(ExpenseCategory))]
    public ExpenseCategory? Category { get; set; }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (Amount.HasValue &&
            decimal.Round(Amount.Value, 2) != Amount.Value)
        {
            yield return new ValidationResult(
                "Amount cannot have more than two decimal places.",
                new[] { nameof(Amount) });
        }

        if (ExpenseDate.HasValue &&
            ExpenseDate.Value == DateOnly.MinValue)
        {
            yield return new ValidationResult(
                "Enter a valid expense date.",
                new[] { nameof(ExpenseDate) });
        }
    }
}