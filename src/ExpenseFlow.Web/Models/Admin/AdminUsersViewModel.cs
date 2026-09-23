using System.ComponentModel.DataAnnotations;

namespace ExpenseFlow.Web.Models.Admin;

public sealed class AdminUsersViewModel
{
    [StringLength(100)]
    public string? Search { get; set; }

    public IReadOnlyList<AdminUserListItemViewModel> Users { get; set; }
        = [];

    public int PageNumber { get; set; }

    public int TotalPages { get; set; }

    public int TotalCount { get; set; }
}

public sealed record AdminUserListItemViewModel(
    string Id,
    string Email,
    bool IsAdmin,
    bool IsManager,
    bool IsFinance,
    bool IsActive,
    bool IsCurrentUser);

public sealed class UpdateUserRolesViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }

    public bool IsManager { get; set; }

    public bool IsFinance { get; set; }
}

public sealed class ChangeUserStatusViewModel
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public bool Activate { get; set; }
}