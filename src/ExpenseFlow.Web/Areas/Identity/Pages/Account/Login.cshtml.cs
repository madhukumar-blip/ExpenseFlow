using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseFlow.Web.Areas.Identity.Pages.Account;

[AllowAnonymous]
public sealed class LoginModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;

    public LoginModel(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
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
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        SetReturnUrl(returnUrl);

        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(ReturnUrl);
        }

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        SetReturnUrl(returnUrl);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Input.Email.Trim());

        if (user is not null)
        {
            var result = await _signInManager.PasswordSignInAsync(
                user,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                return LocalRedirect(ReturnUrl);
            }

            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new
                {
                    ReturnUrl,
                    RememberMe = Input.RememberMe
                });
            }
        }

        ModelState.AddModelError(
            string.Empty,
            "Unable to sign in. Check your details or try again later.");

        return Page();
    }

    private void SetReturnUrl(string? returnUrl)
    {
        ReturnUrl = !string.IsNullOrWhiteSpace(returnUrl) &&
                    Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~/Dashboard");
    }
}