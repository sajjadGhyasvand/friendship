using FriendShip.Application.Dtos;
using FriendShip.Application.Discovery;
using FriendShip.Application.Services;
using FriendShip.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FriendShip.Web.Pages.Ui.Game;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class IndexModel(PersonalGameService games, RoomService rooms, StatsService stats,
    ResultsService results, GuestIdentity identity, FriendKnowledgeService knowledge) : PageModel
{
    [TempData] public string? RecoveryCode { get; set; }
    [TempData] public string? Notice { get; set; }
    public PersonalGameInfo Info { get; private set; } = default!;
    public OwnerRoomDto? Game { get; private set; }
    public InsightsDto? Insights { get; private set; }
    public IReadOnlyList<FriendFact> KnowledgeFacts { get; private set; } = [];
    public StoryContent? Story { get; private set; }
    public IReadOnlyList<LeaderboardEntryDto> Friends { get; private set; } = Array.Empty<LeaderboardEntryDto>();
    public IReadOnlyList<ArchivedGame> Archive { get; private set; } = Array.Empty<ArchivedGame>();
    public string InviteUrl => Url.Page("/Ui/Game/Link", null, new { linkCode = Info.LinkCode }, Request.Scheme)!;
    public async Task<IActionResult> OnGetAsync()
    {
        var caller = identity.EnsureCaller();
        var info = await games.GetMineAsync(caller);
        if (info is null) return NotFound();
        Info = info;
        Archive = await games.GetArchiveAsync(caller);
        if (info.CurrentRoomCode is not null)
        {
            var ownerTask = rooms.GetOwnerViewAsync(caller, info.CurrentRoomCode);
            var statsTask = stats.GetInsightsAsync(caller, info.CurrentRoomCode);
            var friendsTask = results.GetLeaderboardAsync(caller, info.CurrentRoomCode);
            await Task.WhenAll(ownerTask, statsTask, friendsTask);
            Game = await ownerTask;
            Insights = (await statsTask).Value;
            Friends = (await friendsTask).Value ?? Array.Empty<LeaderboardEntryDto>();
            KnowledgeFacts = await knowledge.GetOwnerFactsAsync(caller, info.CurrentRoomCode);
            Story = await knowledge.GetOwnerStoryAsync(caller, info.CurrentRoomCode, info.OwnerName);
        }
        return Page();
    }
    public async Task<IActionResult> OnPostPauseAsync() => await ChangeAsync(false);
    public async Task<IActionResult> OnPostResumeAsync() => await ChangeAsync(true);
    private async Task<IActionResult> ChangeAsync(bool resume)
    {
        var caller = identity.EnsureCaller();
        var info = await games.GetMineAsync(caller);
        if (info?.CurrentRoomCode is null) return NotFound();
        var result = resume ? await rooms.ResumeAsync(caller, info.CurrentRoomCode) : await rooms.PauseAsync(caller, info.CurrentRoomCode);
        Notice = result.Succeeded ? (resume ? "بازی دوباره فعاله." : "بازی موقتاً غیرفعال شد؛ پاسخ‌ها محفوظ‌اند.") : result.Error;
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string code, bool confirmDelete)
    {
        if (!confirmDelete) { Notice = "برای حذف دائمی، تأیید را انتخاب کن."; return RedirectToPage(); }
        var result = await games.DeleteAsync(identity.EnsureCaller(), code);
        Notice = result.Succeeded ? "بازی و پاسخ‌های همان دور حذف شدند." : result.Error;
        return RedirectToPage();
    }
}
