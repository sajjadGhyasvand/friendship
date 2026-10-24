using FriendShip.Application.Dtos;
using FriendShip.Application.Services;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.web.Pages.Ui;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProfileModel(
    GuestIdentity guests,
    ProfileService profiles) : PageModel
{
    public ProfileDto? Profile { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!guests.TryGetCaller(out var caller) ||
            caller.UserId is null)
        {
            ErrorMessage = "هویت حساب در دسترس نیست. دوباره وارد شو.";
            return Page();
        }

        var result = await profiles.GetMineAsync(caller);

        if (!result.Succeeded || result.Value is null)
        {
            ErrorMessage = result.Error ?? "دریافت پروفایل انجام نشد.";
            return Page();
        }

        Profile = result.Value;
        return Page();
    }
}
