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
public sealed class IndexModel(DiscoveryService discovery, GuestIdentity identity) : PageModel
{
    public DiscoveryView View { get; private set; } = default!;
    [TempData] public string? Notice { get; set; }
    public async Task<IActionResult> OnGetAsync()
    {
        var view = await discovery.GetMineAsync(identity.EnsureCaller());
        if (view is null) return NotFound();
        View = view; return Page();
    }
    public async Task<IActionResult> OnPostConfigureAsync(bool enabled, bool comparison, bool relationships, bool mbti, Guid? version, bool aggregateSharing)
    {
        if (!ModelState.IsValid) { Notice = "اطلاعات تنظیمات معتبر نیست."; return RedirectToPage(); }
        var result = await discovery.ConfigureAsync(identity.EnsureCaller(), enabled, comparison, relationships, mbti, version, aggregateSharing);
        Notice = result.Succeeded ? "اجازه‌ها ذخیره شد؛ لینک فکت‌های قبلی لغو شدند." : result.Error;
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostDecisionAsync(string key, AnalysisDecisionState state, string? correction)
    {
        var json = Request.Headers.Accept.Any(h => h?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);
        if (!ModelState.IsValid)
        {
            if (json) return new JsonResult(new { error = "تصمیم معتبر نیست." }) { StatusCode = 400 };
            Notice = "تصمیم معتبر نیست."; return RedirectToPage();
        }
        var result = await discovery.DecideAsync(identity.EnsureCaller(), key, state, correction);
        if (json)
        {
            var portrait = result.Succeeded ? (await discovery.GetMineAsync(identity.EnsureCaller()))?.Analysis?.Portrait : null;
            return new JsonResult(new { succeeded = result.Succeeded, state = state.ToString(), error = result.Error, portrait })
                { StatusCode = result.Succeeded ? 200 : 400 };
        }
        Notice = result.Succeeded ? "نظرت درباره این برداشت ثبت شد." : result.Error;
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostResetAsync(bool confirmReset)
    {
        if (!confirmReset) { Notice = "برای حذف داده‌های کشف، تأیید را انتخاب کن."; return RedirectToPage(); }
        var result = await discovery.ResetAsync(identity.EnsureCaller());
        Notice = result.Succeeded ? "پاسخ‌های تکمیلی، اجازه‌ها و لینک فکت‌ها حذف شدند. بازی‌هایت محفوظ‌اند." : result.Error;
        return RedirectToPage();
    }
}
