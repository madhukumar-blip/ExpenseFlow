using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Domain.Entities;

public sealed class ExpenseAuditEntry
{
    public Guid Id { get; private set; }

    public Guid ExpenseId { get; private set; }

    public string ActorId { get; private set; } = string.Empty;

    public ExpenseAuditAction Action { get; private set; }

    public ExpenseStatus? PreviousStatus { get; private set; }

    public ExpenseStatus? NewStatus { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset OccurredAtUtc { get; private set; }

    private ExpenseAuditEntry()
    {
    }

    public ExpenseAuditEntry(
        Guid expenseId,
        string actorId,
        ExpenseAuditAction action,
        ExpenseStatus? previousStatus = null,
        ExpenseStatus? newStatus = null,
        string? comment = null)
    {
        if (expenseId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid expense ID is required.",
                nameof(expenseId));
        }

        if (string.IsNullOrWhiteSpace(actorId) ||
            actorId.Trim().Length > 450)
        {
            throw new ArgumentException(
                "A valid actor ID is required.",
                nameof(actorId));
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentException(
                "Invalid audit action.",
                nameof(action));
        }

        if (comment?.Trim().Length > 1000)
        {
            throw new ArgumentException(
                "Audit comment cannot exceed 1000 characters.",
                nameof(comment));
        }

        Id = Guid.NewGuid();
        ExpenseId = expenseId;
        ActorId = actorId.Trim();
        Action = action;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        Comment = string.IsNullOrWhiteSpace(comment)
            ? null
            : comment.Trim();
        OccurredAtUtc = DateTimeOffset.UtcNow;
    }
}