using ExpenseFlow.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExpenseFlow.Infrastructure.Persistence;

public sealed class ExpenseFlowDbContext
    : IdentityDbContext<IdentityUser>
{
    public ExpenseFlowDbContext(
        DbContextOptions<ExpenseFlowDbContext> options)
        : base(options)
    {
    }

    public DbSet<Expense> Expenses =>
        Set<Expense>();

    public DbSet<ExpenseAuditEntry> ExpenseAuditEntries =>
        Set<ExpenseAuditEntry>();

    public DbSet<AdministrationAuditEntry>
        AdministrationAuditEntries =>
            Set<AdministrationAuditEntry>();

    public override int SaveChanges()
    {
        ProtectAuditEntries();

        return base.SaveChanges();
    }

    public override int SaveChanges(
        bool acceptAllChangesOnSuccess)
    {
        ProtectAuditEntries();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        ProtectAuditEntries();

        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ProtectAuditEntries();

        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ExpenseFlowDbContext).Assembly);
    }

    private void ProtectAuditEntries()
    {
        var invalidExpenseEntries = ChangeTracker
            .Entries<ExpenseAuditEntry>()
            .Where(entry =>
                entry.State == EntityState.Modified ||
                entry.State == EntityState.Deleted)
            .ToList();

        if (invalidExpenseEntries.Count > 0)
        {
            throw new InvalidOperationException(
                "Expense audit entries are append-only "
                + "and cannot be modified or deleted.");
        }

        var invalidAdministrationEntries = ChangeTracker
            .Entries<AdministrationAuditEntry>()
            .Where(entry =>
                entry.State == EntityState.Modified ||
                entry.State == EntityState.Deleted)
            .ToList();

        if (invalidAdministrationEntries.Count > 0)
        {
            throw new InvalidOperationException(
                "Administration audit entries are append-only "
                + "and cannot be modified or deleted.");
        }
    }
}