using FriendShip.Application.Dtos;
using FriendShip.Application.Discovery;
using FriendShip.Application.Services;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui.Rooms;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ResultsModel(
    GuestIdentity guests,
    ResultsService resultsService, FriendKnowledgeService knowledge) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Code { get; set; } = "";

    public ResultDto? MyResult { get; private set; }
    public KnowledgeAnalysis? Knowledge { get; private set; }

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
                "هویت مهمان در دسترس نیست. نتیجه‌ی خصوصی فقط با هویت شرکت‌کننده قابل مشاهده است.";

            return Page();
        }

        var result = await resultsService.GetMyResultAsync(caller, Code);

        if (!result.Succeeded || result.Value is null)
        {
            ErrorMessage =
                result.Error ?? "دریافت نتیجه انجام نشد.";

            return Page();
        }

        MyResult = result.Value;
        Knowledge = await knowledge.GetMineAsync(caller, Code);

        return Page();
    }
}
