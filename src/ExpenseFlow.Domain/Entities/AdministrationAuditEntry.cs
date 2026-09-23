namespace ExpenseFlow.Domain.Entities;

public sealed class AdministrationAuditEntry
{
    public Guid Id { get; private set; }

    public string ActorId { get; private set; } = string.Empty;

    public string TargetUserId { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string Details { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; private set; }

    private AdministrationAuditEntry()
    {
    }

    public AdministrationAuditEntry(
        string actorId,
        string targetUserId,
        string action,
        string details)
    {
        if (string.IsNullOrWhiteSpace(actorId) ||
            actorId.Length > 450)
        {
            throw new ArgumentException("Invalid administrator.");
        }

        if (string.IsNullOrWhiteSpace(targetUserId) ||
            targetUserId.Length > 450)
        {
            throw new ArgumentException("Invalid target user.");
        }

        if (string.IsNullOrWhiteSpace(action) ||
            action.Trim().Length > 100)
        {
            throw new ArgumentException("Invalid administration action.");
        }

        if (string.IsNullOrWhiteSpace(details) ||
            details.Trim().Length > 1000)
        {
            throw new ArgumentException("Invalid audit details.");
        }

        Id = Guid.NewGuid();
        ActorId = actorId.Trim();
        TargetUserId = targetUserId.Trim();
        Action = action.Trim();
        Details = details.Trim();
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }
}