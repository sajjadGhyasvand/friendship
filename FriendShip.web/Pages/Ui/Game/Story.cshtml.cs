using FriendShip.Application.Discovery;
using FriendShip.Application.Services;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui.Game;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StoryModel(PersonalGameService games, FriendKnowledgeService knowledge, GuestIdentity identity) : PageModel
{
    public StoryContent? Story { get; private set; }
    public async Task<IActionResult> OnGetAsync()
    {
        var caller = identity.EnsureCaller();
        var info = await games.GetMineAsync(caller);
        if (info?.CurrentRoomCode is null) return RedirectToPage("Index");
        Story = await knowledge.GetOwnerStoryAsync(caller, info.CurrentRoomCode, info.OwnerName);
        ViewData["StoryUrl"] = Url.Page("/Ui/Game/Link", null, new { linkCode = info.LinkCode }, Request.Scheme);
        return Page();
    }
}
