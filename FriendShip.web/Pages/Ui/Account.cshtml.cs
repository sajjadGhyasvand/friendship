using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FriendShip.Application.Dtos;
using FriendShip.Application.Services;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountModel(AuthService authService) : PageModel
{
    [BindProperty]
    public AccountInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public bool RegisterMode { get; private set; }

    [TempData] public string? RecoveryCode { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostLoginAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var result = await authService.LoginAsync(
            Input.Email.Trim(),
            Input.Password,
            GetClientIp());

        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Error ?? "ورود انجام نشد.");

            return Page();
        }

        return await CompleteSignInAsync(result.Value);
    }

    public async Task<IActionResult> OnPostRegisterAsync()
    {
        RegisterMode = true;

        if ((Input.Password?.Length ?? 0) < AuthService.PasswordMin)
        {
            ModelState.AddModelError(
                "Input.Password",
                $"رمز عبور باید حداقل {AuthService.PasswordMin} نویسه باشد.");
        }

        if (!ModelState.IsValid)
            return Page();

        var result = await authService.RegisterSimpleAsync(
            Input.Email.Trim(),
            Input.Password ?? "",
            GetClientIp());

        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Error ?? "ثبت‌نام انجام نشد.");

            return Page();
        }

        RecoveryCode = result.Value.RecoveryCode;
        return await CompleteSignInAsync(result.Value.User);
    }

    private async Task<IActionResult> CompleteSignInAsync(AuthUserDto user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(
                AccountCookieEvents.SecurityStampClaim,
                user.SecurityStamp)
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = true
            });

        // جلوگیری از هدایت کاربر به سایت خارجی.
        if (!string.IsNullOrWhiteSpace(ReturnUrl) &&
            Url.IsLocalUrl(ReturnUrl))
        {
            return LocalRedirect(ReturnUrl);
        }

        return RedirectToPage("/Ui/Game/Index");
    }

    private string GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public sealed class AccountInput
    {
        [Required(ErrorMessage = "نام کاربری را وارد کن.")]
        [StringLength(254, ErrorMessage = "نام کاربری یا ایمیل بیش از حد طولانی است.")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "رمز عبور را وارد کن.")]
        [StringLength(100, ErrorMessage = "رمز عبور بیش از حد طولانی است.")]
        public string Password { get; set; } = "";

        public string? DisplayName { get; set; }
    }
}
