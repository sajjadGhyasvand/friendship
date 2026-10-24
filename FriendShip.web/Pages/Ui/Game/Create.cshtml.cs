using FriendShip.Application.Questions;
using FriendShip.Application.Discovery;
using FriendShip.Application.Services;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui.Game;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CreateModel(PersonalGameService games, GuestIdentity identity, DiscoveryService discovery,
    AdaptiveQuestionSelector selector) : PageModel
{
    [BindProperty] public Dictionary<string, int> Answers { get; set; } = new();
    [BindProperty(SupportsGet = true)] public bool NewRound { get; set; }
    public IReadOnlyList<ProfileTemplate> Templates { get; private set; } = [];
    private async Task LoadTemplatesAsync(bool posted = false)
    {
        var caller = identity.EnsureCaller();
        var previous = await games.GetReferenceAnswersAsync(caller);
        var view = await discovery.GetMineAsync(caller);
        var keys = selector.SelectGameKeys(caller.UserId!.Value, view?.Analysis?.Evidence ?? [], NewRound ? previous.Keys : []);
        if (posted && Answers.Count > 0)
            keys = Answers.Keys.Where(k => QuestionFactory.Catalog.Any(t => t.Key == k)).Take(20).Concat(keys).Distinct().Take(Math.Max(10, Math.Min(20, Answers.Count))).ToArray();
        Templates = keys.Select(k => QuestionFactory.Catalog.Single(t => t.Key == k)).ToArray();
    }
    public async Task<IActionResult> OnGetAsync()
    {
        var caller = identity.EnsureCaller();
        var mine = await games.GetMineAsync(caller);
        if (mine?.CurrentRoomCode is not null && !NewRound) return RedirectToPage("/Ui/Game/Index");
        if (NewRound) Answers = new(await games.GetReferenceAnswersAsync(caller));
        await LoadTemplatesAsync();
        Answers = Answers.Where(a => Templates.Any(t => t.Key == a.Key)).ToDictionary(a => a.Key, a => a.Value);
        return Page();
    }
    public async Task<IActionResult> OnPostAsync()
    {
        await LoadTemplatesAsync(true);
        if (!ModelState.IsValid) return Page();
        var result = await games.CreateAsync(identity.EnsureCaller(), Answers, NewRound);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", result.Error!);
            return Page();
        }
        return RedirectToPage("/Ui/Game/Index");
    }
}
