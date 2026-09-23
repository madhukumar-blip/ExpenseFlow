namespace ExpenseFlow.Application.Administration;

public interface IAdministrationAuditWriter
{
    Task WriteAsync(
        string actorId,
        string targetUserId,
        string action,
        string details,
        CancellationToken cancellationToken);
}