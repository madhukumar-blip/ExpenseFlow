using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseFlow.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class RegisterModel : PageModel
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;

    public RegisterModel(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string ReturnUrl { get; private set; } = "/Dashboard";

    public sealed class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(
            128,
            MinimumLength = 10,
            ErrorMessage = "Use between 10 and 128 characters.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(
            nameof(Password),
            ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet(string? returnUrl = null)
    {
        SetReturnUrl(returnUrl);

        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(ReturnUrl);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        SetReturnUrl(returnUrl);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = Input.Email.Trim();

        var user = new IdentityUser
        {
            UserName = email,
            Email = email
        };

        var result = await _userManager.CreateAsync(user, Input.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        if (await _signInManager.CanSignInAsync(user))
        {
            await _signInManager.SignInAsync(
                user,
                isPersistent: false);

            return LocalRedirect(ReturnUrl);
        }

        return RedirectToPage("./Login", new { returnUrl = ReturnUrl });
    }

    private void SetReturnUrl(string? returnUrl)
    {
        ReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) &&
                    Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~/Dashboard");
    }
}