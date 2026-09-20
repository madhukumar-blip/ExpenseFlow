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

    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ExpenseFlowDbContext).Assembly);
    }
}