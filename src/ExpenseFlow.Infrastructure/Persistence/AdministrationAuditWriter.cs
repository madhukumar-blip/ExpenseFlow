using ExpenseFlow.Application.Administration;
using ExpenseFlow.Domain.Entities;

namespace ExpenseFlow.Infrastructure.Persistence;

public sealed class AdministrationAuditWriter
    : IAdministrationAuditWriter
{
    private readonly ExpenseFlowDbContext _dbContext;

    public AdministrationAuditWriter(
        ExpenseFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task WriteAsync(
        string actorId,
        string targetUserId,
        string action,
        string details,
        CancellationToken cancellationToken)
    {
        _dbContext.AdministrationAuditEntries.Add(
            new AdministrationAuditEntry(
                actorId,
                targetUserId,
                action,
                details));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}