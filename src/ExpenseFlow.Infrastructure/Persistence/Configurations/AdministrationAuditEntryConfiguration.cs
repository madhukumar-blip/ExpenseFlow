using ExpenseFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseFlow.Infrastructure.Persistence.Configurations;

public sealed class AdministrationAuditEntryConfiguration
    : IEntityTypeConfiguration<AdministrationAuditEntry>
{
    public void Configure(
        EntityTypeBuilder<AdministrationAuditEntry> builder)
    {
        builder.ToTable("AdministrationAuditEntries");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .ValueGeneratedNever();

        builder.Property(entry => entry.ActorId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(entry => entry.TargetUserId)
            .HasMaxLength(450)
            .IsRequired();

        builder.Property(entry => entry.Action)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(entry => entry.Details)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(entry => entry.OccurredAtUtc)
            .IsRequired();

        builder.HasIndex(entry => entry.OccurredAtUtc);

        builder.HasIndex(entry => new
        {
            entry.TargetUserId,
            entry.OccurredAtUtc
        });
    }
}