using System.Security.Claims;
using ExpenseFlow.Application.Expenses;
using ExpenseFlow.Web.Models.Expenses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseFlow.Web.Controllers;

[Authorize(Roles = "Manager")]
public sealed class ApprovalsController : Controller
{
    private readonly ExpenseReviewService _reviewService;

    public ApprovalsController(ExpenseReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    private string CurrentManagerId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException(
            "The authenticated user has no identifier.");

    [HttpGet]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var expenses = await _reviewService.ListPendingAsync(
            CurrentManagerId,
            cancellationToken);

        return View(expenses);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(
        ReviewExpenseViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || model.Id == Guid.Empty)
        {
            TempData["ErrorMessage"] =
                "Invalid review request. Check the fields and try again.";

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
            var found = await _reviewService.ReviewAsync(
                model.Id,
                CurrentManagerId,
                model.Decision,
                model.Reason,
                expectedRowVersion,
                cancellationToken);

            if (!found)
            {
                return NotFound();
            }

            TempData["SuccessMessage"] =
                model.Decision == ReviewDecision.Approve
                    ? "Expense approved."
                    : "Expense rejected.";
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

    [HttpGet]
    public async Task<IActionResult> PreviewReceipt(
    Guid id,
    CancellationToken cancellationToken)
    {
        var receipt = await _reviewService.GetReceiptAsync(
            id,
            CurrentManagerId,
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
    public async Task<IActionResult> DownloadReceipt(
        Guid id,
        CancellationToken cancellationToken)
    {
        var receipt = await _reviewService.GetReceiptAsync(
            id,
            CurrentManagerId,
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
}