using FriendShip.Application.Dtos;
using FriendShip.Application.Services;
using FriendShip.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui;

[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ShareModel(ResultsService resultsService) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string ShareCode { get; set; } = "";

    public SharedResultDto? SharedResult { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(ShareCode) ||
            ShareCode.Length > 100)
        {
            return NotAvailable();
        }

        try
        {
            SharedResult = await resultsService.GetSharedAsync(ShareCode);
        }
        catch (DomainException)
        {
            return NotAvailable();
        }

        if (SharedResult is null)
            return NotAvailable();

        return Page();
    }

    private PageResult NotAvailable()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;

        ErrorMessage =
            "این کارت پیدا نشد. ممکن است لینک ناقص باشد یا بازی هنوز تمام نشده باشد.";

        return Page();
    }
}
