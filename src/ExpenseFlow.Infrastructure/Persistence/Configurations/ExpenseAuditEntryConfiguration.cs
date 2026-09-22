using ExpenseFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseFlow.Infrastructure.Persistence.Configurations;

public sealed class ExpenseAuditEntryConfiguration
    : IEntityTypeConfiguration<ExpenseAuditEntry>
{
    public void Configure(
        EntityTypeBuilder<ExpenseAuditEntry> builder)
    {
        builder.ToTable("ExpenseAuditEntries", table =>
        {
            table.HasCheckConstraint(
                "CK_ExpenseAuditEntries_Action_Valid",
                "[Action] IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10)");

            table.HasCheckConstraint(
                "CK_ExpenseAuditEntries_PreviousStatus_Valid",
                "[PreviousStatus] IS NULL OR "
                + "[PreviousStatus] IN (1, 2, 3, 4, 5)");

            table.HasCheckConstraint(
                "CK_ExpenseAuditEntries_NewStatus_Valid",
                "[NewStatus] IS NULL OR "
                + "[NewStatus] IN (1, 2, 3, 4, 5)");
        });

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .ValueGeneratedNever();

        builder.Property(entry => entry.ExpenseId)
            .IsRequired();

        builder.Property(entry => entry.ActorId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(entry => entry.Action)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(entry => entry.PreviousStatus)
            .HasConversion<int?>()
            .IsRequired(false);

        builder.Property(entry => entry.NewStatus)
            .HasConversion<int?>()
            .IsRequired(false);

        builder.Property(entry => entry.Comment)
            .HasMaxLength(1000)
            .IsRequired(false);

        builder.Property(entry => entry.OccurredAtUtc)
            .IsRequired();

        builder.HasIndex(entry => new
        {
            entry.ExpenseId,
            entry.OccurredAtUtc
        });

        builder.HasIndex(entry => entry.ActorId);
    }
}