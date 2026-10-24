using FriendShip.Application.Dtos;
using FriendShip.Application.Services;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui.Rooms;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InsightsModel(
    GuestIdentity guests,
    StatsService statsService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Code { get; set; } = "";

    public InsightsDto? Insights { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Code) || Code.Length > 100)
        {
            ErrorMessage = "کد بازی معتبر نیست.";
            return Page();
        }

        if (!guests.TryGetCaller(out var caller) ||
            caller.UserId is null)
        {
            ErrorMessage = "هویت حساب در دسترس نیست. دوباره وارد حساب شو.";
            return Page();
        }

        var result = await statsService.GetInsightsAsync(caller, Code);

        if (!result.Succeeded || result.Value is null)
        {
            ErrorMessage = result.Error ?? "دریافت آمار انجام نشد.";
            return Page();
        }

        Insights = result.Value;

        return Page();
    }
}
