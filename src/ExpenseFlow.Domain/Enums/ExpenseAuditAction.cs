namespace ExpenseFlow.Domain.Enums;

public enum ExpenseAuditAction
{
    Created = 1,
    Updated = 2,
    ReceiptUploaded = 3,
    ReceiptReplaced = 4,
    ReceiptRemoved = 5,
    Submitted = 6,
    Approved = 7,
    Rejected = 8,
    Reimbursed = 9,
    Deleted = 10
}