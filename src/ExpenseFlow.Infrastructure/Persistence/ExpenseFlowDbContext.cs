using ExpenseFlow.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExpenseFlow.Infrastructure.Persistence;

public sealed class ExpenseFlowDbContext : IdentityDbContext<IdentityUser>
{
    public ExpenseFlowDbContext(DbContextOptions<ExpenseFlowDbContext> options)
        : base(options)
    {
    }

    public DbSet<Expense> Expenses => Set<Expense>();

    public DbSet<ExpenseAuditEntry> ExpenseAuditEntries =>
    Set<ExpenseAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ExpenseFlowDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        ProtectAuditEntries();

        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectAuditEntries();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ProtectAuditEntries();

        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ProtectAuditEntries();

        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    private void ProtectAuditEntries()
    {
        var invalidEntries = ChangeTracker
            .Entries<ExpenseAuditEntry>()
            .Where(entry =>
                entry.State == EntityState.Modified ||
                entry.State == EntityState.Deleted)
            .ToList();

        if (invalidEntries.Count > 0)
        {
            throw new InvalidOperationException(
                "Expense audit entries are append-only "
                + "and cannot be modified or deleted.");
        }
    }
}