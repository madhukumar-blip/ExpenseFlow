namespace ExpenseFlow.Application.Expenses;

using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;

public sealed class ExpenseReviewService
{
    private readonly IExpenseStore _store;

    public ExpenseReviewService(IExpenseStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<PendingExpenseItem>> ListPendingAsync(
        string managerId,
        CancellationToken cancellationToken)
    {
        ValidateManagerId(managerId);

        return _store.ListPendingAsync(
            managerId,
            cancellationToken);
    }

    public async Task<bool> ReviewAsync(Guid expenseId, string managerId, ReviewDecision decision, string? comment, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        ValidateManagerId(managerId);

        if (!Enum.IsDefined(typeof(ReviewDecision), decision))
        {
            throw new ArgumentException("Invalid review decision.");
        }

        if (expectedRowVersion is null ||
            expectedRowVersion.Length != 8)
        {
            throw new ArgumentException("Invalid expense version.");
        }

        var expense = await _store.FindForReviewAsync(
            expenseId,
            managerId,
            cancellationToken);

        if (expense is null)
        {
            return false;
        }

        if (!expense.RowVersion.SequenceEqual(expectedRowVersion))
        {
            throw new ExpenseConflictException(
                "This expense has changed. Review the refreshed queue.");
        }

        if (comment?.Trim().Length > 1000)
        {
            throw new ArgumentException(
                "Manager comment cannot exceed 1000 characters.");
        }

        if (decision == ReviewDecision.Approve)
        {
            expense.Approve(managerId);
        }
        else
        {
            expense.Reject(managerId, comment);
        }

        var auditAction = decision == ReviewDecision.Approve
            ? ExpenseAuditAction.Approved
            : ExpenseAuditAction.Rejected;

        var auditComment = decision == ReviewDecision.Approve
            ? comment
            : expense.RejectionReason;

        _store.AddAudit(new ExpenseAuditEntry(
            expense.Id,
            managerId,
            auditAction,
            previousStatus: ExpenseStatus.Submitted,
            newStatus: expense.Status,
            comment: auditComment));

        await _store.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void ValidateManagerId(string managerId)
    {
        if (string.IsNullOrWhiteSpace(managerId))
        {
            throw new ArgumentException("Manager ID is required.");
        }
    }
}