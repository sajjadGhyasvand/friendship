using FriendShip.Application.Common;
using FriendShip.Application.Dtos;
using FriendShip.Application.Services;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui.Rooms;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ManageModel(
    RoomService rooms,
    GuestIdentity identity) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Code { get; set; } = "";

    [TempData]
    public string? Notice { get; set; }

    public OwnerRoomDto Room { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync())
            return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAnswerAsync(
        Guid roomQuestionId,
        Guid optionId)
    {
        if (!await LoadAsync())
            return NotFound();

        if (!ModelState.IsValid ||
            roomQuestionId == Guid.Empty ||
            optionId == Guid.Empty)
        {
            ModelState.AddModelError(
                string.Empty,
                "سؤال و گزینه‌ی معتبر انتخاب کن.");

            return Page();
        }

        var result = await rooms.SetOwnerAnswerAsync(
            identity.EnsureCaller(),
            Code,
            roomQuestionId,
            optionId);

        return await FinishAsync(result, "پاسخ ذخیره شد.");
    }

    public async Task<IActionResult> OnPostPublishAsync()
    {
        if (!await LoadAsync())
            return NotFound();

        var result = await rooms.PublishAsync(
            identity.EnsureCaller(),
            Code);

        return await FinishAsync(
            result,
            "بازی منتشر شد؛ حالا لینک را برای دوستات بفرست.");
    }

    public async Task<IActionResult> OnPostCloseAsync()
    {
        if (!await LoadAsync())
            return NotFound();

        var result = await rooms.CloseAsync(
            identity.EnsureCaller(),
            Code);

        return await FinishAsync(result, "این دور پایان یافت؛ نتایج محفوظ‌اند.");
    }

    private async Task<bool> LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(Code))
            return false;

        var room = await rooms.GetOwnerViewAsync(
            identity.EnsureCaller(),
            Code);

        if (room is null)
            return false;

        Room = room;
        Code = room.Code;

        return true;
    }

    private async Task<IActionResult> FinishAsync(
        Result result,
        string successMessage)
    {
        if (result.Succeeded)
        {
            Notice = successMessage;

            return RedirectToPage(
                "/Ui/Rooms/Manage",
                new { code = Code });
        }

        ModelState.AddModelError(
            string.Empty,
            result.Error ?? "عملیات انجام نشد.");

        if (!await LoadAsync())
            return NotFound();

        return Page();
    }
}
