using ExpenseFlow.Application.Expenses;
using ExpenseFlow.Domain.Entities;
using ExpenseFlow.Domain.Enums;
using ExpenseFlow.Web.Models.Expenses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
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
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest();
        }

        var expense = await _expenseService.GetDraftAsync(
            id,
            CurrentEmployeeId,
            cancellationToken);

        if (expense is null)
        {
            return NotFound();
        }

        return View(new EditExpenseViewModel
        {
            Id = expense.Id,
            Title = expense.Title,
            Description = expense.Description,
            Amount = expense.Amount,
            ExpenseDate = expense.ExpenseDate,
            Category = expense.Category,
            RowVersion = Convert.ToBase64String(expense.RowVersion)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditExpenseViewModel model, CancellationToken cancellationToken)
    {
        byte[] rowVersion = [];

        if (!string.IsNullOrWhiteSpace(model.RowVersion))
        {
            try
            {
                rowVersion = Convert.FromBase64String(model.RowVersion);
            }
            catch (FormatException)
            {
                ModelState.AddModelError(
                    nameof(model.RowVersion),
                    "The expense version is invalid.");
            }
        }

        if (rowVersion.Length != 8)
        {
            ModelState.AddModelError(
                nameof(model.RowVersion),
                "The expense version is invalid.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var command = new UpdateExpenseCommand(
            model.Title,
            model.Description,
            model.Amount!.Value,
            model.ExpenseDate!.Value,
            model.Category!.Value,
            rowVersion);

        try
        {
            var found = await _expenseService.UpdateAsync(
                model.Id,
                CurrentEmployeeId,
                command,
                cancellationToken);

            if (!found)
            {
                return NotFound();
            }

            TempData["SuccessMessage"] =
                "Draft expense updated successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (ExpenseConflictException exception)
        {
            TempData["ErrorMessage"] = exception.Message;

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException)
        {
            TempData["ErrorMessage"] =
                "Only draft expenses can be edited.";

            return RedirectToAction(nameof(Index));
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(
                string.Empty,
                exception.Message);

            return RedirectToAction(nameof(Details), new { id = model.Id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, string? rowVersion, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty ||
            string.IsNullOrWhiteSpace(rowVersion))
        {
            return BadRequest("Invalid delete request.");
        }

        byte[] expectedRowVersion;

        try
        {
            expectedRowVersion =
                Convert.FromBase64String(rowVersion);
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
            var found = await _expenseService.DeleteAsync(
                id,
                CurrentEmployeeId,
                expectedRowVersion,
                cancellationToken);

            if (!found)
            {
                return NotFound();
            }

            TempData["SuccessMessage"] =
                "Draft expense deleted successfully.";
        }
        catch (ExpenseConflictException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (InvalidOperationException)
        {
            TempData["ErrorMessage"] =
                "Only draft expenses can be deleted.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken, string? search = null, ExpenseStatus? status = null, ExpenseCategory? category = null, DateOnly? from = null, DateOnly? to = null, int page = 1)
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
    public async Task<IActionResult> Create(CreateExpenseViewModel model, CancellationToken cancellationToken)
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

        Guid expenseId = Guid.Empty;

        try
        {
            expenseId = await _expenseService.CreateAsync(CurrentEmployeeId, command, cancellationToken);
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

        return RedirectToAction(nameof(Details),
            new { id = expenseId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Guid id, string? rowVersion, CancellationToken cancellationToken)
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
      "Draft created successfully. You can now attach a receipt.";
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5_308_416)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5_308_416)]
    public async Task<IActionResult> UploadReceipt(Guid id, IFormFile? receipt, string? rowVersion, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty ||
            receipt is null ||
            receipt.Length == 0 ||
            string.IsNullOrWhiteSpace(rowVersion))
        {
            TempData["ErrorMessage"] =
                "Select a valid receipt file.";

            return RedirectToAction(
                nameof(Details),
                new { id });
        }

        byte[] expectedRowVersion;

        try
        {
            expectedRowVersion =
                Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            TempData["ErrorMessage"] =
                "The expense version is invalid.";

            return RedirectToAction(
                nameof(Details),
                new { id });
        }

        if (expectedRowVersion.Length != 8)
        {
            TempData["ErrorMessage"] =
                "The expense version is invalid.";

            return RedirectToAction(
                nameof(Details),
                new { id });
        }

        try
        {
            await using var stream = receipt.OpenReadStream();

            var found = await _expenseService.UploadReceiptAsync(
                id,
                CurrentEmployeeId,
                new ReceiptUploadCommand(
                    stream,
                    receipt.FileName,
                    receipt.Length,
                    expectedRowVersion),
                cancellationToken);

            if (!found)
            {
                return NotFound();
            }

            TempData["SuccessMessage"] =
                "Receipt uploaded successfully.";
        }
        catch (ArgumentException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (ExpenseConflictException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }

        return RedirectToAction(
            nameof(Details),
            new { id });
    }

    [HttpGet]
    public async Task<IActionResult> PreviewReceipt(Guid id, CancellationToken cancellationToken)
    {
        var receipt = await _expenseService.DownloadReceiptAsync(
            id,
            CurrentEmployeeId,
            cancellationToken);

        if (receipt is null)
        {
            return NotFound();
        }

        return File(
            receipt.Content,
            receipt.ContentType,
            enableRangeProcessing: true);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadReceipt(Guid id, CancellationToken cancellationToken)
    {
        var receipt = await _expenseService.DownloadReceiptAsync(
            id,
            CurrentEmployeeId,
            cancellationToken);

        if (receipt is null)
        {
            return NotFound();
        }

        return File(
            receipt.Content,
            receipt.ContentType,
            receipt.OriginalFileName,
            enableRangeProcessing: true);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveReceipt(Guid id, string? rowVersion, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty ||
            string.IsNullOrWhiteSpace(rowVersion))
        {
            return BadRequest("Invalid receipt removal request.");
        }

        byte[] expectedRowVersion;

        try
        {
            expectedRowVersion =
                Convert.FromBase64String(rowVersion);
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
            var found = await _expenseService.RemoveReceiptAsync(
                id,
                CurrentEmployeeId,
                expectedRowVersion,
                cancellationToken);

            if (!found)
            {
                return NotFound();
            }

            TempData["SuccessMessage"] =
                "Receipt removed successfully.";
        }
        catch (ExpenseConflictException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }

        return RedirectToAction(
            nameof(Details),
            new { id });
    }
}