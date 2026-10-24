using System.Text.Json;
using FriendShip.Application.Abstractions;
using FriendShip.Application.Common;
using FriendShip.Application.Discovery;
using FriendShip.Application.Questions;
using FriendShip.Application.Services;
using FriendShip.Domain.Common;
using FriendShip.Domain.Discovery;
using FriendShip.Domain.Participants;
using FriendShip.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FriendShip.Tests;

public sealed class PersonalizationTests
{
    private static AnswerEvidence Evidence(string key, int choice = 0)
    {
        var q = DiscoveryCatalog.Find(key)!;
        return new(key, q.Prompt, q.Options[choice], choice, DiscoveryQuestion.Version, "self-discovery", "revision-1", new TestClock().Now.UtcDateTime, q.Sensitive);
    }
    private static DiscoveryService Service(TestDatabase db, TimeProvider clock) => new(db, new DiscoveryEngine(), new AdaptiveQuestionSelector(), new InMemoryRateLimiter(clock), clock);
    private static async Task Configure(DiscoveryService service, Caller caller, bool aggregate = false)
    {
        var view = await service.GetMineAsync(caller);
        Assert.True((await service.ConfigureAsync(caller, true, true, false, false, view?.Settings?.Version, aggregate)).Succeeded);
    }
    private static async Task Answer(DiscoveryService service, Caller caller, string key, int choice = 0)
    {
        var view = (await service.GetMineAsync(caller))!;
        var result = await service.AnswerAsync(caller, key, DiscoveryQuestion.Version, choice, null, false, view.Settings!.Version);
        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public void Combined_portrait_uses_real_cross_category_clues_and_is_stable_and_bounded()
    {
        var engine = new DiscoveryEngine();
        Assert.Equal("یک سرنخِ هدفونی", engine.Analyze([Evidence("music")], false).Portrait!.Nickname);
        var catOnly = engine.Analyze([Evidence("animal")], false);
        Assert.DoesNotContain(catOnly.Facts, f => f.IsCombination);
        var inputs = new[] { Evidence("animal"), Evidence("energy", 3), Evidence("music", 1), Evidence("free-time") };
        var analysis = engine.Analyze(inputs, false);
        Assert.Equal("گربهٔ شیفت شب", analysis.Portrait!.Nickname);
        Assert.NotEmpty(analysis.Portrait.Strengths!); Assert.NotEmpty(analysis.Portrait.SignatureInterests!);
        Assert.NotEmpty(analysis.Portrait.HumorLine); Assert.Equal("animals.cat", analysis.Portrait.VisualKey);
        var combo = Assert.Single(analysis.Facts, f => f.RuleKey == "combo.cat-night");
        Assert.Equal(new[] { "animal", "energy" }, combo.Evidence.Select(e => e.Key).Order().ToArray());
        Assert.Equal(FactCertainty.SupportedPattern, combo.Certainty);
        Assert.Equal(JsonSerializer.Serialize(analysis.Portrait), JsonSerializer.Serialize(engine.Analyze(inputs, false).Portrait));
        Assert.Equal(combo.Text, engine.Analyze(inputs.Reverse().ToArray(), false).Facts.Single(f => f.Id == combo.Id).Text);
        Assert.DoesNotContain(engine.Analyze([Evidence("animal"), Evidence("energy", 0)], false).Facts, f => f.IsCombination);
    }

    [Fact]
    public void Multiple_answer_patterns_are_required_and_rejection_does_not_invent_the_opposite()
    {
        var engine = new DiscoveryEngine();
        var evidence = new[] { Evidence("book-genre"), Evidence("social-weekend", 2), Evidence("social-party", 2), Evidence("social-recharge", 1) };
        var insufficient = engine.Analyze(evidence.Take(3).ToArray(), false);
        Assert.DoesNotContain(insufficient.Facts, f => f.RuleKey == "combo.books-quiet");
        var analysis = engine.Analyze(evidence, false);
        Assert.Equal("مسافرِ بی‌سروصدا", analysis.Portrait!.Nickname);
        Assert.Equal(4, analysis.Facts.Single(f => f.IsCombination).SampleCount);
        var cat = Assert.Single(engine.Analyze([Evidence("animal")], false).Facts);
        var reject = AnalysisDecision.Create(Guid.NewGuid(), cat.Id); reject.Set(AnalysisDecisionState.Rejected, null, DateTime.UtcNow);
        var rejected = engine.Analyze([Evidence("animal")], false, [reject]);
        Assert.Equal("هنوز داریم می‌شناسیمت", rejected.Portrait!.Nickname);
        Assert.DoesNotContain(rejected.Facts, f => f.VisualKey == "animals.dog");
    }

    [Fact]
    public async Task Partial_vote_is_replaceable_idempotent_traced_and_cannot_enter_confirmed_story()
    {
        await using var database = new TestDatabase(); await database.InitializeAsync();
        var owner = await DiscoveryTests.Owner(database, "partial_owner"); var clock = new TestClock(); var service = Service(database, clock);
        await Configure(service, owner); await Answer(service, owner, "animal"); await Answer(service, owner, "energy", 3);
        var fact = Assert.Single((await service.GetMineAsync(owner))!.Analysis!.Facts);
        Assert.True((await service.DecideAsync(owner, fact.Id, AnalysisDecisionState.Confirmed, null)).Succeeded);
        var link = (await service.ShareAsync(owner, fact.Id, true)).Value!;
        clock.Now = clock.Now.AddMinutes(1);
        Assert.True((await service.DecideAsync(owner, fact.Id, AnalysisDecisionState.Partial, null)).Succeeded);
        var partial = (await service.GetMineAsync(owner))!.Analysis!;
        Assert.NotEqual(fact.Title, partial.Portrait!.Nickname);
        Assert.Null(PersonalityComposer.Story("من", partial.Facts)); Assert.Null(await service.GetSharedFactAsync(link));
        await using var db = database.Create();
        var saved = await db.AnalysisDecisions.AsNoTracking().SingleAsync();
        Assert.Equal("combo.cat-night", saved.RuleKey); Assert.Equal(PersonalityComposer.Version, saved.CopyVersion);
        Assert.Equal(2, saved.EvidenceCount); Assert.Equal(clock.Now.UtcDateTime, saved.FeedbackAt);
        Assert.True((await service.DecideAsync(owner, fact.Id, AnalysisDecisionState.Partial, null)).Succeeded);
        Assert.Equal(saved.Revision, (await db.AnalysisDecisions.AsNoTracking().SingleAsync()).Revision);
        Assert.True((await service.DecideAsync(owner, fact.Id, AnalysisDecisionState.Confirmed, null)).Succeeded);
        var story = PersonalityComposer.Story("من", (await service.GetMineAsync(owner))!.Analysis!.Facts)!;
        Assert.Equal(fact.Title, story.Title);
        Assert.Equal(StoryFact.From(fact), Assert.Single(story.Facts));
        Assert.Equal(1, await db.AnalysisDecisions.CountAsync());
        await Answer(service, owner, "energy", 0);
        Assert.False((await service.DecideAsync(owner, fact.Id, AnalysisDecisionState.Confirmed, null)).Succeeded);
    }

    [Fact]
    public void Perception_fact_requires_a_strict_shared_wrong_majority_and_comparable_snapshots()
    {
        var clock = new TestClock(); var room = GameTests.NewRoom(clock); var engine = new KnowledgeEngine();
        var peers = Enumerable.Range(0, 5).Select(i => Complete(room, clock, q => q.TemplateKey == "music" && i < 3 ? 1 : 0)).ToArray();
        var story = engine.Story(room, peers, "من")!;
        var surprise = Assert.Single(story.Facts, f => f.RuleKey.StartsWith("perception."));
        Assert.Equal("perception.music", surprise.RuleKey); Assert.Equal(5, surprise.SampleCount);
        Assert.Equal("music.headphones", surprise.VisualKey);
        var fragmented = Enumerable.Range(0, 5).Select(i => Complete(room, clock, q => q.TemplateKey == "music" ? 1 + i % 3 : 0)).ToArray();
        Assert.DoesNotContain(engine.Story(room, fragmented, "من")!.Facts, f => f.RuleKey.StartsWith("perception."));
        Assert.Null(engine.Story(room, peers.Take(4).ToArray(), "من"));
        foreach (var q in room.Questions) typeof(RoomQuestion).GetProperty(nameof(RoomQuestion.Text))!.SetValue(q, "UNRELATED_QUESTION");
        Assert.Null(engine.Story(room, peers, "من"));
    }

    private static Participant Complete(Room room, TestClock clock, Func<RoomQuestion, int> choice, Guid? user = null)
    {
        var p = Participant.Join(room, 0, Guid.NewGuid(), user, "PRIVATE_FRIEND_NAME", null, ShortCode.Generate(10), clock.Now.UtcDateTime);
        while (!p.IsCompleted)
        {
            var a = p.ServeNext(room, clock.Now.UtcDateTime)!; var q = room.GetQuestion(a.RoomQuestionId);
            var template = QuestionFactory.Catalog.Single(t => t.Key == q.TemplateKey);
            p.SubmitAnswer(room, q.Id, q.Options.Single(o => o.Text == template.Options[choice(q)]).Id, clock.Now.UtcDateTime);
        }
        return p;
    }

    [Fact]
    public void Shared_tastes_use_two_direct_choices_from_five_distinct_peers_not_guesses_or_private_or_stale_answers()
    {
        var engine = new KnowledgeEngine();
        var own = new ConsentedPreferences(Guid.NewGuid(), [Evidence("music"), Evidence("music-region")]);
        var peers = Enumerable.Range(0, 5).Select(_ => new ConsentedPreferences(Guid.NewGuid(), own.Answers)).ToArray();
        var shared = Assert.Single(engine.SharedInterests(own, peers));
        Assert.Equal("shared.music", shared.RuleKey); Assert.Equal(5, shared.SampleCount);
        Assert.Empty(engine.SharedInterests(own, peers.Take(4).ToArray()));
        Assert.Empty(engine.SharedInterests(own, [peers[0], peers[0], peers[0], peers[0], peers[0]]));
        foreach (var invalid in new[] {
            own.Answers.Select(e => e with { Source = "friend-guess" }).ToArray(),
            own.Answers.Select(e => e with { Sensitive = true }).ToArray(),
            own.Answers.Select(e => e with { QuestionVersion = "stale" }).ToArray(),
            own.Answers.Select(e => e with { Value = "forged" }).ToArray(),
            new[] { Evidence("music", 1), Evidence("music-region", 1) },
            new[] { Evidence("social-weekend"), Evidence("social-party") }
        }) Assert.Empty(engine.SharedInterests(own, peers.Take(4).Append(peers[4] with { Answers = invalid }).ToArray()));
        var json = JsonSerializer.Serialize(shared);
        Assert.DoesNotContain(own.UserId.ToString(), json); Assert.DoesNotContain("Evidence", json); Assert.DoesNotContain("OptionIndex", json);
    }

    [Fact]
    public async Task Friends_story_requires_separate_mutual_aggregate_consent_and_revocation_is_immediate()
    {
        await using var database = new TestDatabase(); await database.InitializeAsync();
        var clock = new TestClock(); var service = Service(database, clock); var owner = await DiscoveryTests.Owner(database, "aggregate_owner");
        await Configure(service, owner); await Answer(service, owner, "music"); await Answer(service, owner, "music-region");
        var games = new PersonalGameService(database, new QuestionFactory(), new InMemoryRateLimiter(clock), clock);
        var code = (await games.CreateAsync(owner, GameTests.Answers(), false)).Value!;
        var friends = new List<Caller>();
        for (var i = 0; i < 5; i++)
        {
            var caller = await DiscoveryTests.Owner(database, "aggregate_friend_" + i); friends.Add(caller);
            await Configure(service, caller); await Answer(service, caller, "music"); await Answer(service, caller, "music-region");
            await using var db = database.Create();
            var room = await db.Rooms.Include(r => r.Questions).ThenInclude(q => q.Options).SingleAsync(r => r.Code == code);
            db.Participants.Add(Complete(room, clock, _ => 0, caller.UserId)); await db.SaveChangesAsync();
        }
        var knowledge = new FriendKnowledgeService(database, new KnowledgeEngine());
        async Task<StoryContent> Story() => (await knowledge.GetOwnerStoryAsync(owner, code, "من"))!;
        Assert.DoesNotContain((await Story()).Facts, f => f.RuleKey.StartsWith("shared."));
        await Configure(service, owner, true);
        foreach (var friend in friends.Take(4)) await Configure(service, friend, true);
        Assert.DoesNotContain((await Story()).Facts, f => f.RuleKey.StartsWith("shared."));
        await Configure(service, friends[4], true);
        var story = await Story(); Assert.Single(story.Facts, f => f.RuleKey == "shared.music"); Assert.Equal(4, story.Facts.Count);
        var json = JsonSerializer.Serialize(story);
        Assert.DoesNotContain("PRIVATE_FRIEND_NAME", json);
        foreach (var f in friends) Assert.DoesNotContain(f.UserId.ToString()!, json);
        await Answer(service, owner, "music", 1);
        Assert.DoesNotContain((await Story()).Facts, f => f.RuleKey.StartsWith("shared."));
        await Answer(service, owner, "music");
        Assert.Single((await Story()).Facts, f => f.RuleKey == "shared.music");
        await Configure(service, friends[0]);
        Assert.DoesNotContain((await Story()).Facts, f => f.RuleKey.StartsWith("shared."));
        Assert.Null(await knowledge.GetOwnerStoryAsync(friends[0], code, "مالک جعلی"));
        // Revoking aggregation preserves the responses and the existing private comparison permission.
        var settings = (await service.GetMineAsync(friends[0]))!.Settings!;
        Assert.True(settings.AllowComparison); Assert.False(settings.AllowAggregateSharing);
    }

    [Fact]
    public async Task Quality_report_counts_latest_vote_once_and_separates_partial_and_rejects_non_admins()
    {
        await using var database = new TestDatabase(); await database.InitializeAsync();
        var clock = new TestClock(); var service = Service(database, clock);
        var admin = await DiscoveryTests.Owner(database, "quality_admin");
        await using (var db = database.Create()) { (await db.Users.SingleAsync(u => u.Id == admin.UserId)).PromoteToAdmin(); await db.SaveChangesAsync(); }
        admin = admin with { IsAdmin = true };
        var owners = new List<Caller>();
        for (var i = 0; i < 5; i++)
        {
            var owner = await DiscoveryTests.Owner(database, "quality_voter_" + i); owners.Add(owner);
            await Configure(service, owner); await Answer(service, owner, "animal");
            var fact = Assert.Single((await service.GetMineAsync(owner))!.Analysis!.Facts);
            var state = i < 3 ? AnalysisDecisionState.Confirmed : i == 3 ? AnalysisDecisionState.Partial : AnalysisDecisionState.Rejected;
            Assert.True((await service.DecideAsync(owner, fact.Id, state, null)).Succeeded);
            clock.Now = clock.Now.AddSeconds(1);
            if (i == 3) Assert.Empty(await service.GetFeedbackQualityAsync(admin));
        }
        var report = Assert.Single(await service.GetFeedbackQualityAsync(admin));
        Assert.Equal("animals.cat", report.RuleKey); Assert.Equal(5, report.Voters);
        Assert.Equal(3, report.Confirmed); Assert.Equal(1, report.Partial); Assert.Equal(1, report.Rejected); Assert.Equal(60, report.ConfirmationPercent);
        var first = owners[0]; var oldFact = Assert.Single((await service.GetMineAsync(first))!.Analysis!.Facts);
        await Answer(service, first, "animal", 1); await Answer(service, first, "animal");
        var updated = Assert.Single((await service.GetMineAsync(first))!.Analysis!.Facts); Assert.NotEqual(oldFact.Id, updated.Id);
        Assert.True((await service.DecideAsync(first, updated.Id, AnalysisDecisionState.Rejected, null)).Succeeded);
        // A later publication of an older vote must not make that vote "newer" for quality statistics.
        await using (var db = database.Create())
        {
            (await db.AnalysisDecisions.SingleAsync(d => d.UserId == first.UserId && d.ResultKey == oldFact.Id)).Share(true, clock.Now.AddMinutes(1).UtcDateTime);
            var legacy = AnalysisDecision.Create(first.UserId!.Value, "legacy-vote"); legacy.Set(AnalysisDecisionState.Confirmed, null, clock.Now.UtcDateTime);
            db.AnalysisDecisions.Add(legacy); await db.SaveChangesAsync();
        }
        report = Assert.Single(await service.GetFeedbackQualityAsync(admin));
        Assert.Equal(5, report.Voters); Assert.Equal(2, report.Confirmed); Assert.Equal(2, report.Rejected); Assert.Equal(40, report.ConfirmationPercent);
        Assert.Empty(await service.GetFeedbackQualityAsync(first));
        Assert.Empty(await service.GetFeedbackQualityAsync(first with { IsAdmin = true }));
        Assert.Empty(await service.GetFeedbackQualityAsync(admin with { IsAdmin = false }));
        var output = JsonSerializer.Serialize(report); Assert.DoesNotContain(first.UserId.ToString()!, output); Assert.DoesNotContain("ResultKey", output);
    }
}
