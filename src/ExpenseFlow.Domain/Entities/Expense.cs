using System;
using System.Collections.Generic;
using System.Text;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Domain.Entities;

public class Expense
{
    public Guid Id { get; private set; }

    public string EmployeeId { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }

    public DateOnly ExpenseDate { get; private set; }

    public ExpenseStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? SubmittedAtUtc { get; private set; }

    public string? ReviewedById { get; private set; }

    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    public string? RejectionReason { get; private set; }

    public string? ReimbursedById { get; private set; }

    public DateTimeOffset? ReimbursedAtUtc { get; private set; }

    public string? PaymentReference { get; private set; }

    public string? ReceiptStoredFileName { get; private set; }

    public string? ReceiptOriginalFileName { get; private set; }

    public string? ReceiptContentType { get; private set; }

    public long? ReceiptSize { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public ExpenseCategory Category { get; private set; }
    = ExpenseCategory.Other;

    // EF Core can use this constructor when loading saved expenses.
    private Expense()
    {
    }

    public Expense(string employeeId, string title, string description, decimal amount, DateOnly expenseDate)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
        {
            throw new ArgumentException(
                "Employee ID is required.",
                nameof(employeeId));
        }

        ValidateDetails(title, description, amount, expenseDate);

        Id = Guid.NewGuid();
        EmployeeId = employeeId.Trim();
        Title = title.Trim();
        Description = description.Trim();
        Amount = amount;
        ExpenseDate = expenseDate;
        Status = ExpenseStatus.Draft;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void Reimburse(string financeUserId, string paymentReference)
    {
        if (string.IsNullOrWhiteSpace(financeUserId) ||
            financeUserId.Length > 450)
        {
            throw new ArgumentException("A valid finance user is required.");
        }

        if (EmployeeId == financeUserId)
        {
            throw new InvalidOperationException(
                "You cannot reimburse your own expense.");
        }

        if (Status != ExpenseStatus.Approved)
        {
            throw new InvalidOperationException(
                "Only approved expenses can be reimbursed.");
        }

        if (string.IsNullOrWhiteSpace(paymentReference) ||
            paymentReference.Trim().Length > 100)
        {
            throw new ArgumentException(
                "Enter a payment reference between 1 and 100 characters.");
        }

        Status = ExpenseStatus.Reimbursed;
        ReimbursedById = financeUserId;
        ReimbursedAtUtc = DateTimeOffset.UtcNow;
        PaymentReference = paymentReference.Trim();
    }

    public void UpdateDraft(string title, string description, decimal amount, DateOnly expenseDate)
    {
        EnsureDraft();

        ValidateDetails(title, description, amount, expenseDate);

        Title = title.Trim();
        Description = description.Trim();
        Amount = amount;
        ExpenseDate = expenseDate;
    }

    public void Submit()
    {
        EnsureDraft();

        Status = ExpenseStatus.Submitted;
        SubmittedAtUtc = DateTimeOffset.UtcNow;
    }

    private void EnsureDraft()
    {
        if (Status != ExpenseStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only draft expenses can be changed.");
        }
    }

    public void AttachReceipt(string storedFileName, string originalFileName, string contentType, long size)
    {
        EnsureDraft();

        if (string.IsNullOrWhiteSpace(storedFileName) ||
            storedFileName.Trim().Length > 100)
        {
            throw new ArgumentException(
                "A valid stored receipt filename is required.",
                nameof(storedFileName));
        }

        if (string.IsNullOrWhiteSpace(originalFileName) ||
            originalFileName.Trim().Length > 255)
        {
            throw new ArgumentException(
                "The original receipt filename must contain "
                + "between 1 and 255 characters.",
                nameof(originalFileName));
        }

        if (string.IsNullOrWhiteSpace(contentType) ||
            contentType.Trim().Length > 100)
        {
            throw new ArgumentException(
                "A valid receipt content type is required.",
                nameof(contentType));
        }

        if (size <= 0 || size > 5 * 1024 * 1024)
        {
            throw new ArgumentException(
                "The receipt must be between 1 byte and 5 MB.",
                nameof(size));
        }

        ReceiptStoredFileName = storedFileName.Trim();
        ReceiptOriginalFileName = originalFileName.Trim();
        ReceiptContentType = contentType.Trim();
        ReceiptSize = size;
    }

    public void RemoveReceipt()
    {
        EnsureDraft();

        ReceiptStoredFileName = null;
        ReceiptOriginalFileName = null;
        ReceiptContentType = null;
        ReceiptSize = null;
    }

    public void ChangeCategory(ExpenseCategory category)
    {
        EnsureDraft();

        if (!Enum.IsDefined(typeof(ExpenseCategory), category))
        {
            throw new ArgumentException(
                "Select a valid expense category.",
                nameof(category));
        }

        Category = category;
    }

    public void Approve(string reviewerId)
    {
        EnsureCanReview(reviewerId);

        Status = ExpenseStatus.Approved;
        ReviewedById = reviewerId;
        ReviewedAtUtc = DateTimeOffset.UtcNow;
        RejectionReason = null;
    }

    public void Reject(string reviewerId, string? reason)
    {
        EnsureCanReview(reviewerId);

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
       "A rejection reason is required.");
        }

        if (reason.Trim().Length > 1000)
        {
            throw new ArgumentException(
                "Rejection reason cannot exceed 1000 characters.",
                nameof(reason));
        }

        Status = ExpenseStatus.Rejected;
        ReviewedById = reviewerId;
        ReviewedAtUtc = DateTimeOffset.UtcNow;
        RejectionReason = reason.Trim();
    }

    private void EnsureCanReview(string reviewerId)
    {
        if (string.IsNullOrWhiteSpace(reviewerId) ||
            reviewerId.Length > 450)
        {
            throw new ArgumentException(
                "A valid reviewer ID is required.",
                nameof(reviewerId));
        }

        if (EmployeeId == reviewerId)
        {
            throw new InvalidOperationException(
                "You cannot review your own expense.");
        }

        if (Status != ExpenseStatus.Submitted)
        {
            throw new InvalidOperationException(
                "Only submitted expenses can be reviewed.");
        }
    }

    private static void ValidateDetails(string title, string description, decimal amount, DateOnly expenseDate)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException(
                "Title is required.",
                nameof(title));
        }

        if (title.Trim().Length > 150)
        {
            throw new ArgumentException(
                "Title cannot exceed 150 characters.",
                nameof(title));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException(
                "Description is required.",
                nameof(description));
        }

        if (description.Trim().Length > 1000)
        {
            throw new ArgumentException(
                "Description cannot exceed 1000 characters.",
                nameof(description));
        }

        if (amount <= 0)
        {
            throw new ArgumentException(
                "Amount must be greater than zero.",
                nameof(amount));
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException(
                "Amount cannot have more than two decimal places.",
                nameof(amount));
        }

        if (expenseDate == default)
        {
            throw new ArgumentException(
                "Expense date is required.",
                nameof(expenseDate));
        }

        if (amount > 9999999999999999.99m)
        {
            throw new ArgumentException(
                "Amount exceeds the supported limit.",
                nameof(amount));
        }
    }
}
