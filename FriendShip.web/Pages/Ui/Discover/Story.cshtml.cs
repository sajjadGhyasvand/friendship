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
public sealed class StoryModel(DiscoveryService discovery, GuestIdentity identity) : PageModel
{
    public StoryContent? Story { get; private set; }
    public IReadOnlyList<AnalysisFact> Available { get; private set; } = [];
    public IReadOnlySet<string> Selected { get; private set; } = new HashSet<string>();
    public async Task<IActionResult> OnGetAsync(string[]? fact, bool choose = false)
    {
        var view = await discovery.GetMineAsync(identity.EnsureCaller());
        if (view?.Analysis is null) return RedirectToPage("Index");
        Available = view.Analysis.Facts.Where(f => !f.Sensitive && f.CanShare && f.Decision == AnalysisDecisionState.Confirmed &&
            !string.IsNullOrWhiteSpace(f.RuleKey) && f.CategoryKey != FactCategory.Other).ToArray();
        // An explicit empty selection stays empty; arbitrary or another owner's IDs cannot add content.
        Selected = (choose ? Available.Where(f => (fact ?? []).Contains(f.Id)) : Available).Take(4)
            .Select(f => f.Id).ToHashSet(StringComparer.Ordinal);
        Story = PersonalityComposer.Story(view.OwnerName, view.Analysis.Facts, Selected);
        ViewData["StoryUrl"] = Url.Page("/Ui/Index", null, null, Request.Scheme);
        return Page();
    }
}
