using System.Security.Claims;
using ExpenseFlow.Application.Administration;
using ExpenseFlow.Web.Models.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseFlow.Web.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AdminController : Controller
{
    private static readonly string[] ManagedRoles =
        ["Admin", "Manager", "Finance"];

    private readonly UserManager<IdentityUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly IAdministrationAuditWriter _auditWriter;

    public AdminController(
        UserManager<IdentityUser> users,
        RoleManager<IdentityRole> roles,
        SignInManager<IdentityUser> signInManager,
        IAdministrationAuditWriter auditWriter)
    {
        _users = users;
        _roles = roles;
        _signInManager = signInManager;
        _auditWriter = auditWriter;
    }

    private string CurrentUserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException(
            "Authenticated user identifier is unavailable.");

    [HttpGet]
    public async Task<IActionResult> Users(
        string? search,
        int page = 1)
    {
        const int pageSize = 20;

        search = search?.Trim();

        if (search?.Length > 100)
        {
            ModelState.AddModelError(
                nameof(search),
                "Search cannot exceed 100 characters.");
        }

        var query = _users.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(user =>
                (user.Email != null &&
                 user.Email.Contains(search)) ||
                (user.UserName != null &&
                 user.UserName.Contains(search)));
        }

        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(
            1,
            (int)Math.Ceiling((double)totalCount / pageSize));

        page = Math.Clamp(page, 1, totalPages);

        var users = await query
            .OrderBy(user => user.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = new List<AdminUserListItemViewModel>();

        foreach (var user in users)
        {
            var userRoles = await _users.GetRolesAsync(user);
            var active = !user.LockoutEnd.HasValue ||
                         user.LockoutEnd <= DateTimeOffset.UtcNow;

            items.Add(new AdminUserListItemViewModel(
                user.Id,
                user.Email ?? user.UserName ?? "Unavailable",
                userRoles.Contains("Admin"),
                userRoles.Contains("Manager"),
                userRoles.Contains("Finance"),
                active,
                user.Id == CurrentUserId));
        }

        return View(new AdminUsersViewModel
        {
            Search = search,
            Users = items,
            PageNumber = page,
            TotalPages = totalPages,
            TotalCount = totalCount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRoles(
        UpdateUserRolesViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Invalid role request.";
            return RedirectToAction(nameof(Users));
        }

        var user = await _users.FindByIdAsync(model.UserId);

        if (user is null)
        {
            return NotFound();
        }

        var requestedRoles = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        if (model.IsAdmin) requestedRoles.Add("Admin");
        if (model.IsManager) requestedRoles.Add("Manager");
        if (model.IsFinance) requestedRoles.Add("Finance");

        var currentRoles = await _users.GetRolesAsync(user);

        if (user.Id == CurrentUserId &&
            !requestedRoles.Contains("Admin"))
        {
            TempData["ErrorMessage"] =
                "You cannot remove your own Admin role.";

            return RedirectToAction(nameof(Users));
        }

        if (currentRoles.Contains("Admin") &&
            !requestedRoles.Contains("Admin"))
        {
            var administrators =
                await _users.GetUsersInRoleAsync("Admin");

            if (administrators.Count <= 1)
            {
                TempData["ErrorMessage"] =
                    "The final Admin role cannot be removed.";

                return RedirectToAction(nameof(Users));
            }
        }

        foreach (var role in ManagedRoles)
        {
            if (!await _roles.RoleExistsAsync(role))
            {
                EnsureSucceeded(await _roles.CreateAsync(
                    new IdentityRole(role)));
            }

            var currentlyAssigned = currentRoles.Contains(role);
            var shouldBeAssigned = requestedRoles.Contains(role);

            if (shouldBeAssigned && !currentlyAssigned)
            {
                EnsureSucceeded(await _users.AddToRoleAsync(user, role));
            }
            else if (!shouldBeAssigned && currentlyAssigned)
            {
                EnsureSucceeded(await _users.RemoveFromRoleAsync(user, role));
            }
        }

        await _auditWriter.WriteAsync(
            CurrentUserId,
            user.Id,
            "Roles updated",
            $"Roles changed from [{string.Join(", ", currentRoles)}] "
            + $"to [{string.Join(", ", requestedRoles)}].",
            cancellationToken);

        if (user.Id == CurrentUserId)
        {
            await _signInManager.RefreshSignInAsync(user);
        }

        TempData["SuccessMessage"] =
            $"Roles updated for {user.Email}.";

        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(
        ChangeUserStatusViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        var user = await _users.FindByIdAsync(model.UserId);

        if (user is null)
        {
            return NotFound();
        }

        if (user.Id == CurrentUserId && !model.Activate)
        {
            TempData["ErrorMessage"] =
                "You cannot deactivate your own account.";

            return RedirectToAction(nameof(Users));
        }

        EnsureSucceeded(await _users.SetLockoutEnabledAsync(
            user,
            true));

        EnsureSucceeded(await _users.SetLockoutEndDateAsync(
            user,
            model.Activate
                ? null
                : DateTimeOffset.MaxValue));

        EnsureSucceeded(await _users.UpdateSecurityStampAsync(user));

        await _auditWriter.WriteAsync(
            CurrentUserId,
            user.Id,
            model.Activate
                ? "Account activated"
                : "Account deactivated",
            model.Activate
                ? "User account access restored."
                : "User account access disabled.",
            cancellationToken);

        TempData["SuccessMessage"] = model.Activate
            ? $"Account activated for {user.Email}."
            : $"Account deactivated for {user.Email}.";

        return RedirectToAction(nameof(Users));
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join(
                    "; ",
                    result.Errors.Select(error => error.Description)));
        }
    }
}