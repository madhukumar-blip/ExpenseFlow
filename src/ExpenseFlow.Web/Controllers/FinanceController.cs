using System.Security.Claims;
using ExpenseFlow.Application.Expenses;
using ExpenseFlow.Web.Models.Expenses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseFlow.Web.Controllers;

[Authorize(Roles = "Finance")]
public sealed class FinanceController : Controller
{
    private readonly ExpenseFinanceService _financeService;

    public FinanceController(ExpenseFinanceService financeService)
    {
        _financeService = financeService;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException(
            "The authenticated user has no identifier.");

    [HttpGet]
    public async Task<IActionResult> PreviewReceipt(Guid id, CancellationToken cancellationToken)
    {
        var receipt = await _financeService.GetReceiptAsync(
            id,
            CurrentUserId,
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
        var receipt = await _financeService.GetReceiptAsync(
            id,
            CurrentUserId,
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

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        return View(await _financeService.ListApprovedAsync(
            CurrentUserId,
            cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reimburse(ReimburseExpenseViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || model.Id == Guid.Empty)
        {
            TempData["ErrorMessage"] =
                "Enter a payment reference of at most 100 characters.";

            return RedirectToAction(nameof(Index));
        }

        byte[] expectedRowVersion;

        try
        {
            expectedRowVersion =
                Convert.FromBase64String(model.RowVersion);
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
            var found = await _financeService.ReimburseAsync(
                model.Id,
                CurrentUserId,
                model.PaymentReference,
                expectedRowVersion,
                cancellationToken);

            if (!found)
            {
                return NotFound();
            }

            TempData["SuccessMessage"] =
                "Reimbursement recorded successfully.";
        }
        catch (ExpenseConflictException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (ArgumentException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}