using System.Security.Claims;
using ExpenseFlow.Application.Expenses;
using ExpenseFlow.Web.Models.Expenses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ExpenseFlow.Domain.Enums;

namespace ExpenseFlow.Web.Controllers;

[Authorize]
public sealed class ExpensesController : Controller
{
    private readonly ExpenseService _expenseService;
    private readonly ExpenseReportingService _reportingService;

    public ExpensesController(ExpenseService expenseService, ExpenseReportingService reportingService)
    {
        _expenseService = expenseService;
        _reportingService = reportingService;
    }

    private string CurrentEmployeeId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException(
            "The authenticated user has no identifier.");

    public async Task<IActionResult> Details(
    Guid id,
    CancellationToken cancellationToken)
    {
        var expense = await _reportingService.GetDetailsAsync(
            id,
            CurrentEmployeeId,
            cancellationToken);

        if (expense is null)
        {
            return NotFound();
        }

        return View(expense);
    }

    [HttpGet]
    public async Task<IActionResult> Index(
       CancellationToken cancellationToken,
       string? search = null,
       ExpenseStatus? status = null,
       ExpenseCategory? category = null,
       DateOnly? from = null,
       DateOnly? to = null,
       int page = 1)
    {
        var model = new ExpenseIndexViewModel
        {
            Search = search,
            Status = status,
            Category = category,
            From = from,
            To = to
        };

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            model.Results = await _expenseService.SearchAsync(
                CurrentEmployeeId,
                new ExpenseSearch(
                    search,
                    status,
                    category,
                    from,
                    to,
                    page),
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(
                string.Empty,
                exception.Message);
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateExpenseViewModel
        {
            ExpenseDate = DateOnly.FromDateTime(DateTime.Today)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateExpenseViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var command = new CreateExpenseCommand(
            model.Title,
            model.Description,
            model.Amount!.Value,
            model.ExpenseDate!.Value,
            model.Category!.Value);

        try
        {
            await _expenseService.CreateAsync(
                CurrentEmployeeId,
                command,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(
                string.Empty,
                exception.Message);

            return View(model);
        }

        TempData["SuccessMessage"] =
            "Expense saved successfully as a draft.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(
        Guid id,
        string? rowVersion,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid ||
            id == Guid.Empty ||
            string.IsNullOrWhiteSpace(rowVersion))
        {
            return BadRequest("Invalid submission request.");
        }

        byte[] expectedRowVersion;

        try
        {
            expectedRowVersion = Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            return BadRequest("Invalid expense version.");
        }

        if (expectedRowVersion.Length != 8)
        {
            return BadRequest("Invalid expense version.");
        }

        try
        {
            var found = await _expenseService.SubmitAsync(
                id,
                CurrentEmployeeId,
                expectedRowVersion,
                cancellationToken);

            if (!found)
            {
                return NotFound();
            }

            TempData["SuccessMessage"] =
                "Expense submitted for approval.";
        }
        catch (ExpenseConflictException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (InvalidOperationException)
        {
            TempData["ErrorMessage"] =
                "Only draft expenses can be submitted.";
        }

        return RedirectToAction(nameof(Index));
    }
}