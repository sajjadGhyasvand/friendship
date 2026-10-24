using FriendShip.Application.Abstractions;
using FriendShip.Application.Common;
using FriendShip.Application.Discovery;
using FriendShip.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace FriendShip.Application.Services;

public sealed class FriendKnowledgeService(IAppDbContextFactory dbf, KnowledgeEngine engine)
{
    public async Task<StoryContent?> GetOwnerStoryAsync(Caller caller, string code, string owner)
    {
        if (caller.UserId is not Guid id) return null;
        await using var db = dbf.Create();
        var game = await db.Rooms.AsNoTracking().Include(r => r.Questions).ThenInclude(q => q.Options)
            .SingleOrDefaultAsync(r => r.Code == ShortCode.Normalize(code) && r.OwnerUserId == id);
        if (game is null || !await db.Users.AnyAsync(u => u.Id == id && !u.IsBanned)) return null;
        var completed = await db.Participants.AsNoTracking().Include(p => p.Answers)
            .Where(p => p.RoomId == game.Id && p.CompletedAt != null).ToListAsync();
        var story = engine.Story(game, completed, owner);
        if (story is null) return null;
        var peerIds = completed.Where(p => p.UserId.HasValue && p.UserId != id).Select(p => p.UserId!.Value).Distinct().ToArray();
        if (peerIds.Length < KnowledgeEngine.MinimumStoryParticipants) return story;
        var ids = peerIds.Append(id).ToArray();
        // Anonymous guesses never become a friend's tastes. Explicit aggregate consent is separate from private comparison.
        var profiles = await db.DiscoveryProfiles.AsNoTracking().Where(p => ids.Contains(p.UserId) && p.Enabled && p.AllowAggregateSharing &&
            db.Users.Any(u => u.Id == p.UserId && !u.IsBanned)).ToListAsync();
        var mine = profiles.FirstOrDefault(p => p.UserId == id);
        if (mine is null || profiles.Count < KnowledgeEngine.MinimumStoryParticipants + 1) return story;
        var allowed = profiles.Select(p => p.UserId).ToArray();
        var answers = await db.DiscoveryAnswers.AsNoTracking().Where(a => allowed.Contains(a.UserId)).ToListAsync();
        var discovery = new DiscoveryEngine();
        ConsentedPreferences Preferences(FriendShip.Domain.Discovery.DiscoveryProfile p) => new(p.UserId,
            discovery.ReadEvidence(answers.Where(a => a.UserId == p.UserId), null, p)
                // Do not mix a newly edited preference with the contradictory reference of this round.
                .Where(e => p.UserId != id || !game.Questions.Any(q => q.TemplateKey == e.Key &&
                    q.Options.FirstOrDefault(o => o.Id == q.OwnerAnswerOptionId)?.Text != e.Value)).ToArray());
        var shared = engine.SharedInterests(Preferences(mine), profiles.Where(p => p.UserId != id).Select(Preferences).ToArray());
        return shared.Count == 0 ? story : story with { Facts = story.Facts.Take(3).Concat(shared).Take(4).ToArray() };
    }
    public async Task<KnowledgeAnalysis?> GetMineAsync(Caller caller, string code)
    {
        await using var db = dbf.Create();
        var game = await db.Rooms.AsNoTracking().Include(r => r.Questions).ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(r => r.Code == ShortCode.Normalize(code));
        if (game is null) return null;
        var participant = await db.Participants.AsNoTracking().Include(p => p.Answers).FirstOrDefaultAsync(p =>
            p.RoomId == game.Id && p.VisitorId == caller.VisitorId && p.CompletedAt != null);
        return participant is null ? null : engine.Analyze(game, participant.Answers);
    }

    public async Task<IReadOnlyList<FriendFact>> GetOwnerFactsAsync(Caller caller, string code)
    {
        if (caller.UserId is not Guid id) return [];
        await using var db = dbf.Create();
        var game = await db.Rooms.AsNoTracking().Include(r => r.Questions).ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(r => r.Code == ShortCode.Normalize(code) && r.OwnerUserId == id);
        if (game is null) return [];
        var completed = await db.Participants.AsNoTracking().Include(p => p.Answers)
            .Where(p => p.RoomId == game.Id && p.CompletedAt != null).ToListAsync();
        return engine.OwnerFacts(game, completed);
    }
}
