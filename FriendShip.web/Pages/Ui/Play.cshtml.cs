using FriendShip.Application.Dtos;
using FriendShip.Application.Services;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlayModel(
    GuestIdentity guests,
    PlayService playService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Code { get; set; } = "";

    [BindProperty]
    public Guid RoomQuestionId { get; set; }

    [BindProperty]
    public Guid? OptionId { get; set; }

    [BindProperty]
    public bool TimedOut { get; set; }
    [BindProperty] public bool Skip { get; set; }

    public PlayQuestionDto? Question { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!IsValidCode())
            return Failure("کد بازی معتبر نیست.");

        if (!guests.TryGetCaller(out var caller))
        {
            return RedirectToPage(
                "/Ui/Rooms/Join",
                new { code = Code });
        }

        var result = await playService.GetStateAsync(caller, Code);

        if (!result.Succeeded || result.Value is null)
        {
            return Failure(
                result.Error ?? "دریافت وضعیت بازی انجام نشد.");
        }

        if (result.Value.Finished)
        {
            return RedirectToPage(
    "/Ui/Rooms/Results",
    new { code = Code });

        }

        if (result.Value.Question is null)
            return Failure("سؤال قابل نمایش پیدا نشد.");

        Question = result.Value.Question;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!IsValidCode())
            return Failure("کد بازی معتبر نیست.");

        if (!guests.TryGetCaller(out var caller))
        {
            return Failure(
                "هویت مهمان در دسترس نیست. از صفحه ورود به بازی دوباره وارد شو.");
        }

        if (!ModelState.IsValid || RoomQuestionId == Guid.Empty)
            return Failure("اطلاعات درخواست معتبر نیست.");

        if (!TimedOut && !Skip &&
            (OptionId is null || OptionId == Guid.Empty))
        {
            return Failure("برای ثبت پاسخ، یک گزینه انتخاب کن.");
        }

        // A timeout submits no option. The domain determines the outcome.
        var result = await playService.SubmitAsync(
            caller,
            Code,
            RoomQuestionId,
            TimedOut || Skip ? null : OptionId);

        if (!result.Succeeded)
        {
            // Do not automatically resubmit or serve another question here.
            // The user can explicitly retrieve the persisted state.
            return Failure(
                result.Error ?? "ثبت پاسخ انجام نشد.");
        }

        // POST / Redirect / GET prevents ordinary refresh from resubmitting.
        return RedirectToPage(
            result.Value ? "/Ui/Rooms/Results" : "/Ui/Play",
            new { code = Code });
    }

    private bool IsValidCode() =>
        !string.IsNullOrWhiteSpace(Code) && Code.Length <= 100;

    private PageResult Failure(string message)
    {
        ErrorMessage = message;
        Question = null;

        return Page();
    }
}
