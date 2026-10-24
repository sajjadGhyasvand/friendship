using System.ComponentModel.DataAnnotations;
using FriendShip.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui;

[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RecoveryModel(AuthService auth) : PageModel
{
    [BindProperty, Required, StringLength(30)] public string UserName { get; set; } = "";
    [BindProperty, Required, StringLength(24, MinimumLength = 24)] public string Code { get; set; } = "";
    [BindProperty, Required, StringLength(100, MinimumLength = 8)] public string Password { get; set; } = "";
    public string? NewCode { get; private set; }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var result = await auth.RecoverAsync(UserName, Code, Password, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        if (!result.Succeeded) ModelState.AddModelError("", result.Error!);
        else NewCode = result.Value;
        return Page();
    }
}
