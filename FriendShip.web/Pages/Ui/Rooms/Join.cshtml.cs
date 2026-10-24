using FriendShip.Application.Services;
using FriendShip.Domain.Participants;
using FriendShip.Domain.Users;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using FriendShip.Application.Dtos;

namespace FriendShip.Web.Pages.Ui.Rooms;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class JoinModel(
    GuestIdentity guests,
    PlayService playService,
    RoomService roomService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    [Required(ErrorMessage = "کد بازی را وارد کن.")]
    [StringLength(100, ErrorMessage = "کد واردشده بیش از حد طولانی است.")]
    public string Code { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "نام نمایشی را وارد کن.")]
    [StringLength(
        Participant.NameMax,
        MinimumLength = Participant.NameMin,
        ErrorMessage = "نام نمایشی باید بین ۲ تا ۳۰ حرف باشد.")]
    public string DisplayName { get; set; } = "";

    [BindProperty]
    public string AvatarKey { get; set; } = AvatarCatalog.DefaultKey;

    public IReadOnlyList<AvatarInfo> Avatars => AvatarCatalog.All;
    public RoomPublicDto? Game { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var caller = guests.EnsureCaller();
        if (!string.IsNullOrWhiteSpace(Code))
        {
            Game = await roomService.GetPublicAsync(caller, Code);
            if (Game is null) return NotFound();
        }

        // An empty initial form is not a validation failure.
        ModelState.Clear();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Do not silently issue a different identity during a join POST.
        if (!guests.TryGetCaller(out var caller))
        {
            ModelState.AddModelError(
                string.Empty,
                "هویت مهمان در دسترس نیست. صفحه را دوباره باز کن و اجازه‌ی کوکی بده.");

            return Page();
        }

        Game = await roomService.GetPublicAsync(caller, Code);

        if (!AvatarCatalog.IsValid(AvatarKey))
        {
            ModelState.AddModelError(
                nameof(AvatarKey),
                "یکی از آواتارهای فهرست را انتخاب کن.");
        }

        if (!ModelState.IsValid)
            return Page();

        Code = Code.Trim();
        DisplayName = DisplayName.Trim();

        var room = await roomService.GetPublicAsync(caller, Code);

        if (room is null)
        {
            ModelState.AddModelError(
                string.Empty,
                "این بازی پیدا نشد یا قابل دسترسی نیست.");

            return Page();
        }

        // Availability and participant rules are enforced by the service/domain.
        // Do not duplicate those rules in the Razor page.
        var result = await playService.JoinAsync(
            caller,
            room.Code,
            DisplayName,
            AvatarKey);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Error ?? "ورود به بازی انجام نشد.");

            return Page();
        }

        return RedirectToPage(
      "/Ui/Play",
      new { code = room.Code });
    }
}
