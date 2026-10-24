using FriendShip.Application.Discovery;
using FriendShip.Application.Services;
using FriendShip.Domain.Discovery;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui.Discover;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FactsModel(DiscoveryService discovery, GuestIdentity identity) : PageModel
{
    [TempData] public string? Notice { get; set; }
    public IActionResult OnGet() => RedirectToPage("Index");
    public async Task<IActionResult> OnPostDecisionAsync(string key, AnalysisDecisionState state)
    {
        if (!ModelState.IsValid) { Notice = "تصمیم معتبر نیست."; return RedirectToPage(); }
        var result = await discovery.DecideAsync(identity.EnsureCaller(), key, state, null);
        Notice = result.Succeeded ? "نظرت ثبت شد. انتشار قبلی این فکت لغو شد." : result.Error;
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostShareAsync(string key, bool allow)
    {
        if (!ModelState.IsValid) { Notice = "درخواست معتبر نیست."; return RedirectToPage("Settings"); }
        var result = await discovery.ShareAsync(identity.EnsureCaller(), key, allow);
        Notice = result.Succeeded ? (allow ? "فقط همین فکت عمومی شد؛ لینک و کارت آن آماده است." : "لینک انتشار این فکت لغو شد.") : result.Error;
        return RedirectToPage("Settings");
    }
}
