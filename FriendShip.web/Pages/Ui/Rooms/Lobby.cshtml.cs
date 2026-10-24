using FriendShip.Application.Dtos;
using FriendShip.Application.Services;
using FriendShip.Domain.Common;
using FriendShip.Domain.Rooms;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui.Rooms;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LobbyModel(
    RoomService rooms,
    GuestIdentity guests) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Code { get; set; } = "";

    public RoomPublicDto Room { get; private set; } = default!;

    public string StatusText => Room.Status switch
    {
        RoomStatus.Draft => "در حال آماده‌سازی",
        RoomStatus.Open => "باز",
        RoomStatus.Closed => "بسته‌شده",
        RoomStatus.Expired => "مهلت تمام شده",
        RoomStatus.Cancelled => "لغوشده",
        RoomStatus.Paused => "توقف موقت",
        _ => "نامشخص"
    };

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Code))
            return BadRequest("کد بازی را وارد کن.");

        Code = ShortCode.Normalize(Code);

        if (!ShortCode.IsValid(Code, FriendShip.Domain.Rooms.Room.CodeLength) &&
            !ShortCode.IsValid(Code, FriendShip.Domain.Rooms.Room.LegacyCodeLength))
            return BadRequest("کد بازی معتبر نیست.");

        var room = await rooms.GetPublicAsync(
            guests.EnsureCaller(),
            Code);

        if (room is null)
            return NotFound();

        Room = room;
        Code = room.Code;

        return Page();
    }
}
