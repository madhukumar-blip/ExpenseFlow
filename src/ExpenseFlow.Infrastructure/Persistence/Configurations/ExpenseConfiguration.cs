using ExpenseFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseFlow.Infrastructure.Persistence.Configurations;

public sealed class ExpenseConfiguration
    : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses", table =>
        {
            table.HasCheckConstraint(
                "CK_Expenses_Amount_Positive",
                "[Amount] > 0");

            table.HasCheckConstraint(
                "CK_Expenses_Status_Valid",
                "[Status] IN (1, 2, 3, 4, 5)");

            table.HasCheckConstraint(
                "CK_Expenses_Category_Valid",
                "[Category] IN (0, 1, 2, 3, 4)");

            table.HasCheckConstraint(
                 "CK_Expenses_Receipt_Metadata",
                 "("
               + "[ReceiptStoredFileName] IS NULL "
               + "AND [ReceiptOriginalFileName] IS NULL "
               + "AND [ReceiptContentType] IS NULL "
               + "AND [ReceiptSize] IS NULL"
               + ") OR ("
               + "[ReceiptStoredFileName] IS NOT NULL "
               + "AND [ReceiptOriginalFileName] IS NOT NULL "
               + "AND [ReceiptContentType] IS NOT NULL "
               + "AND [ReceiptSize] > 0 "
               + "AND [ReceiptSize] <= 5242880"
               + ")");
               });

        builder.Property(expense => expense.ReimbursedById)
            .HasMaxLength(450);

        builder.Property(expense => expense.PaymentReference)
            .HasMaxLength(100);

        builder.Property(expense => expense.ReviewedById)
            .HasMaxLength(450);

        builder.Property(expense => expense.RejectionReason)
            .HasMaxLength(1000);

        builder.HasIndex(expense => new
        {
            expense.Status,
            expense.SubmittedAtUtc
        });

        builder.HasKey(expense => expense.Id);

        builder.Property(expense => expense.Id)
            .ValueGeneratedNever();

        builder.Property(expense => expense.EmployeeId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(expense => expense.Title)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(expense => expense.Description)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(expense => expense.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(expense => expense.ExpenseDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(expense => expense.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(expense => expense.CreatedAtUtc)
            .IsRequired();

        builder.Property(expense => expense.SubmittedAtUtc)
            .IsRequired(false);

        builder.Property(expense => expense.RowVersion)
            .IsRowVersion();

        builder.Property(expense => expense.Category)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(expense => expense.ReceiptStoredFileName)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(expense => expense.ReceiptOriginalFileName)
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(expense => expense.ReceiptContentType)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(expense => expense.ReceiptSize)
            .IsRequired(false);

        builder.HasIndex(expense => new
        {
            expense.EmployeeId,
            expense.Status
        });
    }
}