using System.Security.Claims;
using ExpenseFlow.Application.Expenses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseFlow.Web.Controllers;

[Authorize]
public sealed class DashboardController : Controller
{
    private readonly ExpenseReportingService _reportingService;

    public DashboardController(ExpenseReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException(
                "The authenticated user has no identifier.");

        return View(await _reportingService.GetSummaryAsync(
            userId,
            cancellationToken));
    }
}