using FriendShip.Application.Dtos;
using FriendShip.Application.Services;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui.Rooms;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LeaderboardModel(
    GuestIdentity guests,
    ResultsService resultsService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Code { get; set; } = "";

    public IReadOnlyList<LeaderboardEntryDto> Entries { get; private set; }
        = Array.Empty<LeaderboardEntryDto>();

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Code) || Code.Length > 100)
        {
            ErrorMessage = "کد بازی معتبر نیست.";
            return Page();
        }

        if (!guests.TryGetCaller(out var caller))
        {
            ErrorMessage =
                "برای مشاهده‌ی جدول، با حساب صاحب بازی وارد شو یا ابتدا در بازی شرکت کن.";

            return Page();
        }

        var result = await resultsService.GetLeaderboardAsync(caller, Code);

        if (!result.Succeeded || result.Value is null)
        {
            ErrorMessage = result.Error
                ?? "دریافت جدول رتبه‌بندی انجام نشد.";

            return Page();
        }

        Entries = result.Value;

        return Page();
    }
}
