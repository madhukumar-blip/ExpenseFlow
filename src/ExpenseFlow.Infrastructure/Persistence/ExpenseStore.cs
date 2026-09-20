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

    public async Task<PagedExpenses> SearchAsync(
    string employeeId,
    ExpenseSearch filter,
    CancellationToken cancellationToken)
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

    public async Task AddAsync(
        Expense expense,
        CancellationToken cancellationToken)
    {
        _dbContext.Expenses.Add(expense);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseListItem>>
        ListForEmployeeAsync(
            string employeeId,
            CancellationToken cancellationToken)
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

    public Task<Expense?> FindOwnedAsync(
        Guid expenseId,
        string employeeId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Expenses.SingleOrDefaultAsync(
            expense =>
                expense.Id == expenseId &&
                expense.EmployeeId == employeeId,
            cancellationToken);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken)
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

    public async Task<IReadOnlyList<FinanceExpenseItem>> ListApprovedAsync(
    string financeUserId,
    CancellationToken cancellationToken)
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

    public Task<Expense?> FindForReimbursementAsync(
        Guid expenseId,
        string financeUserId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Expenses.SingleOrDefaultAsync(
            expense =>
                expense.Id == expenseId &&
                expense.EmployeeId != financeUserId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseStatusSummary>> GetSummaryAsync(
        string employeeId,
        CancellationToken cancellationToken)
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

    public async Task<ExpenseDetails?> GetOwnedDetailsAsync(
        Guid expenseId,
        string employeeId,
        CancellationToken cancellationToken)
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

        var actorIds = new[]
        {
        expense.EmployeeId,
        expense.ReviewedById,
        expense.ReimbursedById
    }
        .Where(id => !string.IsNullOrWhiteSpace(id))
        .Select(id => id!)
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

        string ActorName(string? id)
        {
            return id is not null &&
                   actors.TryGetValue(id, out var name)
                ? name
                : "Unavailable account";
        }

        var history = new List<ExpenseHistoryEntry>
    {
        new(
            "Created",
            expense.CreatedAtUtc,
            ActorName(expense.EmployeeId),
            "Expense created as a draft.")
    };

        if (expense.SubmittedAtUtc is { } submittedAt)
        {
            history.Add(new ExpenseHistoryEntry(
                "Submitted",
                submittedAt,
                ActorName(expense.EmployeeId),
                "Submitted for manager approval."));
        }

        if (expense.ReviewedAtUtc is { } reviewedAt)
        {
            var rejected = expense.Status == ExpenseStatus.Rejected;

            history.Add(new ExpenseHistoryEntry(
                rejected ? "Rejected" : "Approved",
                reviewedAt,
                ActorName(expense.ReviewedById),
                rejected
                    ? expense.RejectionReason
                    : "Approved for reimbursement."));
        }

        if (expense.ReimbursedAtUtc is { } reimbursedAt)
        {
            history.Add(new ExpenseHistoryEntry(
                "Reimbursement recorded",
                reimbursedAt,
                ActorName(expense.ReimbursedById),
                $"Payment reference: {expense.PaymentReference}"));
        }

        return new ExpenseDetails(
            expense.Id,
            expense.Title,
            expense.Description,
            expense.Category,
            expense.Amount,
            expense.ExpenseDate,
            expense.Status,
            history.OrderBy(entry => entry.OccurredAtUtc).ToList());
    }

    public async Task<IReadOnlyList<PendingExpenseItem>> ListPendingAsync(
    string managerId,
    CancellationToken cancellationToken)
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

    public Task<Expense?> FindForReviewAsync(
        Guid expenseId,
        string managerId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Expenses.SingleOrDefaultAsync(
            expense =>
                expense.Id == expenseId &&
                expense.EmployeeId != managerId,
            cancellationToken);
    }
}