using FriendShip.Application.Discovery;
using FriendShip.Application.Services;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui.Discover;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class QuestionModel(DiscoveryService discovery, AdaptiveQuestionSelector selector, GuestIdentity identity) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Key { get; set; }
    [BindProperty(SupportsGet = true)] public string? Category { get; set; }
    [BindProperty] public string QuestionVersion { get; set; } = DiscoveryQuestion.Version;
    [BindProperty] public Guid Version { get; set; }
    [BindProperty] public int? Option { get; set; }
    [BindProperty] public string? Text { get; set; }
    public DiscoveryQuestion? Question { get; private set; }
    public DiscoveryView View { get; private set; } = default!;
    [TempData] public string? Notice { get; set; }
    public async Task<IActionResult> OnGetAsync()
    {
        var caller = identity.EnsureCaller();
        var view = await discovery.GetMineAsync(caller);
        if (view?.Settings is not { Enabled: true } settings || view.Analysis is null) return RedirectToPage("Index");
        View = view; Version = settings.Version;
        Question = Key is not null ? DiscoveryCatalog.Find(Key) : selector.Next(caller.UserId!.Value,
            view.Analysis.Evidence, view.Answers.Select(a => a.QuestionKey), settings.AllowRelationships, settings.AllowMbti, Category);
        if (Question is not null && (Question.Sensitive && !settings.AllowRelationships || Question.MbtiOnly && !settings.AllowMbti)) return NotFound();
        if (Key is not null && Question is null) return NotFound();
        if (Question is not null)
        {
            Key = Question.Key;
            var previous = view.Analysis.Evidence.FirstOrDefault(e => e.Key == Key);
            Option = previous?.OptionIndex; Text = Question.FreeText ? previous?.Value : null;
        }
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(bool skip)
    {
        if (!ModelState.IsValid || Key is null) { Notice = "اطلاعات درخواست معتبر نیست."; return RedirectToPage(new { key = Key, category = Category }); }
        var result = await discovery.AnswerAsync(identity.EnsureCaller(), Key, QuestionVersion, Option, Text, skip, Version);
        Notice = result.Succeeded ? (skip ? "این سؤال کنار گذاشته شد؛ به‌عنوان پاسخ اشتباه حساب نمی‌شود." : "انتخابت ذخیره شد؛ بریم سراغ بعدی؟") : result.Error;
        return RedirectToPage(new { key = result.Succeeded ? null : Key, category = Category });
    }
}
