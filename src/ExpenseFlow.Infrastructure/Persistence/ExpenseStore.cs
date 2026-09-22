using ExpenseFlow.Application.Expenses;
using ExpenseFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Infrastructure.Persistence;

public sealed class ExpenseStore : IExpenseStore
{
    private readonly ExpenseFlowDbContext _dbContext;

    public ExpenseStore(ExpenseFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedExpenses> SearchAsync(string employeeId, ExpenseSearch filter, CancellationToken cancellationToken)
    {
        const int pageSize = 10;

        var query = _dbContext.Expenses
            .AsNoTracking()
            .Where(expense => expense.EmployeeId == employeeId);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();

            query = query.Where(expense =>
                expense.Title.Contains(search) ||
                expense.Description.Contains(search));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(expense =>
                expense.Status == filter.Status.Value);
        }

        if (filter.Category.HasValue)
        {
            query = query.Where(expense =>
                expense.Category == filter.Category.Value);
        }

        if (filter.From.HasValue)
        {
            query = query.Where(expense =>
                expense.ExpenseDate >= filter.From.Value);
        }

        if (filter.To.HasValue)
        {
            query = query.Where(expense =>
                expense.ExpenseDate <= filter.To.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var totalPages = Math.Max(
            1,
            (int)Math.Ceiling((double)totalCount / pageSize));

        var pageNumber = Math.Clamp(
            filter.PageNumber,
            1,
            totalPages);

        var items = await query
            .OrderByDescending(expense => expense.CreatedAtUtc)
            .ThenByDescending(expense => expense.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(expense => new ExpenseListItem(
                expense.Id,
                expense.Title,
                expense.Category,
                expense.Amount,
                expense.ExpenseDate,
                expense.Status,
                expense.RowVersion,
                expense.RejectionReason))
            .ToListAsync(cancellationToken);

        return new PagedExpenses(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }

    public Task AddAsync(Expense expense, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _dbContext.Expenses.Add(expense);

        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ExpenseListItem>> ListForEmployeeAsync(string employeeId, CancellationToken cancellationToken)
    {
        return await _dbContext.Expenses
            .AsNoTracking()
            .Where(expense => expense.EmployeeId == employeeId)
            .OrderByDescending(expense => expense.CreatedAtUtc)
            .ThenByDescending(expense => expense.Id)
            .Take(100)
            .Select(expense => new ExpenseListItem(
                 expense.Id,
    expense.Title,
    expense.Category,
    expense.Amount,
    expense.ExpenseDate,
    expense.Status,
    expense.RowVersion,
    expense.RejectionReason))
            .ToListAsync(cancellationToken);
    }

    public Task<Expense?> FindOwnedAsync(Guid expenseId, string employeeId, CancellationToken cancellationToken)
    {
        return _dbContext.Expenses.SingleOrDefaultAsync(
            expense =>
                expense.Id == expenseId &&
                expense.EmployeeId == employeeId,
            cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ExpenseConflictException(
                "Another action changed this expense. "
                + "Review the refreshed list.",
                exception);
        }
    }

    public async Task<IReadOnlyList<FinanceExpenseItem>> ListApprovedAsync(string financeUserId, CancellationToken cancellationToken)
    {
        return await (
            from expense in _dbContext.Expenses.AsNoTracking()
            join user in _dbContext.Users.AsNoTracking()
                on expense.EmployeeId equals user.Id into employees
            from employee in employees.DefaultIfEmpty()
            where expense.Status == ExpenseStatus.Approved
                && expense.EmployeeId != financeUserId
            orderby expense.ReviewedAtUtc, expense.Id
            select new FinanceExpenseItem(
                expense.Id,
                employee == null
                    ? "Unavailable account"
                    : employee.Email ?? "No email",
                expense.Title,
                expense.Amount,
                expense.ReviewedAtUtc,
                expense.RowVersion))
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    public Task<Expense?> FindForReimbursementAsync(Guid expenseId, string financeUserId, CancellationToken cancellationToken)
    {
        return _dbContext.Expenses.SingleOrDefaultAsync(
            expense =>
                expense.Id == expenseId &&
                expense.EmployeeId != financeUserId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseStatusSummary>> GetSummaryAsync(string employeeId, CancellationToken cancellationToken)
    {
        return await _dbContext.Expenses
            .AsNoTracking()
            .Where(expense => expense.EmployeeId == employeeId)
            .GroupBy(expense => expense.Status)
            .Select(group => new ExpenseStatusSummary(
                group.Key,
                group.Count(),
                group.Sum(expense => expense.Amount)))
            .ToListAsync(cancellationToken);
    }

    public async Task<ExpenseDetails?> GetOwnedDetailsAsync(Guid expenseId, string employeeId, CancellationToken cancellationToken)
    {
        var expense = await _dbContext.Expenses
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == expenseId &&
                        item.EmployeeId == employeeId,
                cancellationToken);

        if (expense is null)
        {
            return null;
        }

        var auditEntries = await _dbContext.ExpenseAuditEntries
   .AsNoTracking()
   .Where(entry => entry.ExpenseId == expenseId)
   .OrderBy(entry => entry.OccurredAtUtc)
   .ThenBy(entry => entry.Id)
   .ToListAsync(cancellationToken);

        var actorIds = auditEntries
            .Select(entry => entry.ActorId)
            .Distinct()
            .ToArray();

        var actors = await _dbContext.Users
            .AsNoTracking()
            .Where(user => actorIds.Contains(user.Id))
            .Select(user => new
            {
                user.Id,
                Name = user.Email ?? user.UserName ?? user.Id
            })
            .ToDictionaryAsync(
                user => user.Id,
                user => user.Name,
                cancellationToken);

        string ActorName(string actorId)
        {
            return actors.TryGetValue(actorId, out var name)
                ? name
                : "Unavailable account";
        }

        var history = auditEntries
            .Select(entry => new ExpenseHistoryEntry(
                FormatAuditAction(entry.Action),
                entry.OccurredAtUtc,
                ActorName(entry.ActorId),
                entry.Comment))
            .ToList();

        if (history.Count == 0)
        {
            history.Add(new ExpenseHistoryEntry(
                "Existing expense",
                expense.CreatedAtUtc,
                "System",
                $"Current status: {expense.Status}."));
        }

        return new ExpenseDetails(
            expense.Id,
            expense.Title,
            expense.Description,
            expense.Category,
            expense.Amount,
            expense.ExpenseDate,
            expense.Status,
            expense.ReceiptOriginalFileName,
            expense.ReceiptContentType,
            expense.ReceiptSize,
            expense.RowVersion,
            history);
    }

    public async Task<IReadOnlyList<PendingExpenseItem>> ListPendingAsync(string managerId, CancellationToken cancellationToken)
    {
        return await (
            from expense in _dbContext.Expenses.AsNoTracking()
            join user in _dbContext.Users.AsNoTracking()
                on expense.EmployeeId equals user.Id into employees
            from employee in employees.DefaultIfEmpty()
            where expense.Status == ExpenseStatus.Submitted
                && expense.EmployeeId != managerId
            orderby expense.SubmittedAtUtc, expense.Id
            select new PendingExpenseItem(
                expense.Id,
                employee == null
                    ? "Unavailable account"
                    : employee.Email ?? "No email",
                expense.Title,
                expense.Description,
                expense.Category,
                expense.Amount,
                expense.ExpenseDate,
                expense.RowVersion))
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    public Task<Expense?> FindForReviewAsync(Guid expenseId, string managerId, CancellationToken cancellationToken)
    {
        return _dbContext.Expenses.SingleOrDefaultAsync(
            expense =>
                expense.Id == expenseId &&
                expense.EmployeeId != managerId,
            cancellationToken);
    }

    public Task<ExpenseDraft?> GetOwnedDraftAsync(Guid expenseId, string employeeId, CancellationToken cancellationToken)
    {
        return _dbContext.Expenses
            .AsNoTracking()
            .Where(expense =>
                expense.Id == expenseId &&
                expense.EmployeeId == employeeId &&
                expense.Status == ExpenseStatus.Draft)
            .Select(expense => new ExpenseDraft(
                expense.Id,
                expense.Title,
                expense.Description,
                expense.Amount,
                expense.ExpenseDate,
                expense.Category,
                expense.RowVersion))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public void Remove(Expense expense)
    {
        _dbContext.Expenses.Remove(expense);
    }

    public void AddAudit(ExpenseAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _dbContext.ExpenseAuditEntries.Add(entry);
    }

    private static string FormatAuditAction(ExpenseAuditAction action)
    {
        return action switch
        {
            ExpenseAuditAction.Created => "Expense created",
            ExpenseAuditAction.Updated => "Draft updated",
            ExpenseAuditAction.ReceiptUploaded => "Receipt uploaded",
            ExpenseAuditAction.ReceiptReplaced => "Receipt replaced",
            ExpenseAuditAction.ReceiptRemoved => "Receipt removed",
            ExpenseAuditAction.Submitted => "Submitted for approval",
            ExpenseAuditAction.Approved => "Expense approved",
            ExpenseAuditAction.Rejected => "Expense rejected",
            ExpenseAuditAction.Reimbursed => "Reimbursement recorded",
            ExpenseAuditAction.Deleted => "Draft deleted",
            _ => action.ToString()
        };
    }
}