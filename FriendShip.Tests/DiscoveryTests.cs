using FriendShip.Application.Common;
using FriendShip.Application.Abstractions;
using FriendShip.Application.Discovery;
using FriendShip.Application.Questions;
using FriendShip.Application.Services;
using FriendShip.Domain.Discovery;
using FriendShip.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FriendShip.Tests;

public sealed class DiscoveryTests
{
    private static AnswerEvidence Evidence(string key, int choice = 0)
    {
        var q = DiscoveryCatalog.Find(key)!;
        return new(key, q.Prompt, q.Options[choice], choice, DiscoveryQuestion.Version,
            "self-discovery", "revision-1", new TestClock().Now.UtcDateTime, q.Sensitive);
    }
    private static DiscoveryService Service(TestDatabase db) => new(db, new DiscoveryEngine(),
        new AdaptiveQuestionSelector(), new InMemoryRateLimiter(TimeProvider.System), TimeProvider.System);
    internal static async Task<Caller> Owner(TestDatabase db, string name = "discovery_owner")
    {
        var auth = new AuthService(db, new PasswordService(), new InMemoryRateLimiter(TimeProvider.System), TimeProvider.System);
        var result = await auth.RegisterSimpleAsync(name, "DiscoveryPass!123", "test");
        Assert.True(result.Succeeded, result.Error);
        return new(result.Value!.User.Id, Guid.NewGuid(), "test", false);
    }
    private static async Task Configure(DiscoveryService service, Caller caller, bool comparison = false, bool relationships = false, bool mbti = false)
    {
        var view = await service.GetMineAsync(caller);
        var result = await service.ConfigureAsync(caller, true, comparison, relationships, mbti, view?.Settings?.Version);
        Assert.True(result.Succeeded, result.Error);
    }
    private static async Task Answer(DiscoveryService service, Caller caller, string key, int? option = 0, string? text = null, bool skip = false)
    {
        var view = (await service.GetMineAsync(caller))!;
        var result = await service.AnswerAsync(caller, key, DiscoveryQuestion.Version, option, text, skip, view.Settings!.Version);
        Assert.True(result.Succeeded, result.Error);
    }

    [Fact]
    public void Catalog_is_versioned_unique_and_shared_questions_exclude_private_content()
    {
        Assert.True(DiscoveryCatalog.All.Count > 40);
        Assert.Equal(DiscoveryCatalog.All.Count, DiscoveryCatalog.All.Select(q => q.Key).Distinct().Count());
        Assert.Equal(QuestionFactory.Catalog.Count, QuestionFactory.Catalog.Select(q => q.Key).Distinct().Count());
        Assert.All(DiscoveryCatalog.All.Where(q => q.Scores is not null), q => Assert.Equal(q.Options.Length, q.Scores!.Length));
        Assert.All(QuestionFactory.Catalog, q =>
        {
            var definition = DiscoveryCatalog.Find(q.Key)!;
            Assert.False(definition.Sensitive); Assert.False(definition.MbtiOnly); Assert.False(definition.FreeText);
            Assert.InRange(q.Options.Length, 2, 6);
            Assert.All(q.FriendPrompts, p => Assert.NotEqual(q.Prompt, p));
        });
    }
    [Fact]
    public void Traits_need_three_independent_answers_and_artist_is_never_guessed()
    {
        var engine = new DiscoveryEngine();
        var few = engine.Analyze([Evidence("social-weekend"), Evidence("social-party")], false);
        Assert.Null(few.Traits.Single(t => t.Key == "EI").Percent);
        Assert.DoesNotContain(few.Facts, f => f.Kind == "برداشت احتمالی");
        var enough = engine.Analyze([Evidence("social-weekend"), Evidence("social-party"), Evidence("social-recharge"), Evidence("music", 1)], false);
        Assert.Equal(100, enough.Traits.Single(t => t.Key == "EI").Percent);
        Assert.Contains(enough.Facts, f => f.Kind == "برداشت احتمالی" && f.SampleCount == 3);
        Assert.DoesNotContain(enough.Interests, i => i.Key == "music.artist");
        Assert.Contains(enough.Interests, i => i.Key == "music.genre" && i.Value == "راک" && i.Source == "اظهار مستقیم");
        var duplicate = engine.Analyze([Evidence("social-weekend"), Evidence("social-weekend"), Evidence("social-weekend")], false);
        Assert.Equal(1, duplicate.Traits.Single(t => t.Key == "EI").EvidenceCount);
        Assert.Null(duplicate.Traits.Single(t => t.Key == "EI").Percent);
    }
    [Fact]
    public void Guesses_unknown_versions_and_malformed_values_cannot_become_self_preferences()
    {
        var engine = new DiscoveryEngine(); var music = Evidence("music");
        var invalid = new[] { music with { Source = "friend-guess" }, music with { QuestionVersion = "unknown" },
            music with { Value = "unregistered artist" }, music with { Prompt = "Changed meaning" } };
        var result = engine.Analyze(invalid, true);
        Assert.Empty(result.Evidence); Assert.Empty(result.Interests); Assert.Empty(result.Facts);
        Assert.Null(result.Mbti!.Type);
    }
    [Fact]
    public void Mbti_needs_each_axis_and_balanced_answers_do_not_produce_a_type()
    {
        var engine = new DiscoveryEngine();
        var all = DiscoveryCatalog.All.Where(q => q.Dimension is "EI" or "SN" or "TF" or "JP").Select(q => Evidence(q.Key)).ToArray();
        Assert.Equal(12, all.Length);
        Assert.Equal("ESTJ", engine.Analyze(all, true).Mbti!.Type);
        Assert.Null(engine.Analyze(all.Where(e => e.Key != "detail-new").ToArray(), true).Mbti!.Type);
        Assert.Null(engine.Analyze(all, false).Mbti);
        var balanced = all.Where(e => !e.Key.StartsWith("social-")).Concat(new[] {
            Evidence("social-weekend", 0), Evidence("social-party", 2), Evidence("social-recharge", 2) }).ToArray();
        Assert.Equal(50, engine.Analyze(balanced, true).Traits.Single(t => t.Key == "EI").Percent);
        Assert.Null(engine.Analyze(balanced, true).Mbti!.Type);
    }
    [Fact]
    public void Facts_are_repeatable_traceable_and_decisions_depend_on_evidence_revision()
    {
        var engine = new DiscoveryEngine(); var inputs = new[] { Evidence("music"), Evidence("music-context") };
        var fact = Assert.Single(engine.Analyze(inputs, false).Facts);
        Assert.Equal(2, fact.SampleCount); Assert.Equal(2, fact.Evidence.Count);
        var repeated = Assert.Single(engine.Analyze(inputs, false).Facts);
        Assert.Equal(fact.Id, repeated.Id); Assert.Equal(fact.Text, repeated.Text);
        var decision = AnalysisDecision.Create(Guid.NewGuid(), fact.Id); decision.Set(AnalysisDecisionState.Confirmed, null, DateTime.UtcNow);
        Assert.Equal(AnalysisDecisionState.Confirmed, engine.Analyze(inputs, false, [decision]).Facts[0].Decision);
        var changed = inputs.Select(e => e.Key == "music" ? e with { Revision = "revision-2" } : e).ToArray();
        var newFact = Assert.Single(engine.Analyze(changed, false, [decision]).Facts);
        Assert.NotEqual(fact.Id, newFact.Id); Assert.Null(newFact.Decision);
        var privateFact = Assert.Single(engine.Analyze([Evidence("romance-value")], false).Facts);
        Assert.True(privateFact.Sensitive); Assert.False(privateFact.CanShare);
        var mislabeled = Assert.Single(engine.Analyze([Evidence("romance-value") with { Sensitive = false }], false).Facts);
        Assert.True(mislabeled.Sensitive); Assert.False(mislabeled.CanShare);
    }
    [Fact]
    public void Selector_respects_consent_followups_skips_and_variety()
    {
        var selector = new AdaptiveQuestionSelector(); var id = Guid.NewGuid();
        var initial = selector.Available([], [], false, false);
        Assert.DoesNotContain(initial, q => q.Sensitive || q.MbtiOnly || q.FollowKey is not null);
        Assert.Contains(selector.Available([Evidence("free-time")], [], false, false), q => q.Key == "game-style");
        Assert.DoesNotContain(selector.Available([Evidence("free-time", 1)], [], false, false), q => q.Key == "game-style");
        Assert.DoesNotContain(selector.Available([], ["romance-message"], true, true), q => q.Sensitive);
        var next = selector.Next(id, [], [], false, false)!;
        Assert.NotEqual(next.Key, selector.Next(id, [], [next.Key], false, false)!.Key);
        Assert.Equal(next.Key, selector.Next(id, [], [], false, false)!.Key);
        var chosen = selector.SelectGameKeys(id, [], []);
        Assert.Equal(10, chosen.Distinct().Count());
        Assert.True(chosen.Select(k => DiscoveryCatalog.Find(k)!.Category).Distinct().Count() >= 3);
        Assert.True(chosen.GroupBy(k => DiscoveryCatalog.Find(k)!.Category).Count(g => g.Count() >= 2) >= 3);
        var alternatives = Enumerable.Range(0, 12).Select(_ => string.Join(",", selector.SelectGameKeys(Guid.NewGuid(), [], []))).Distinct().Count();
        Assert.True(alternatives > 1);
        Assert.NotEqual(string.Join(",", chosen), string.Join(",", selector.SelectGameKeys(id, [], chosen)));
    }
    [Fact]
    public async Task Analysis_is_opt_in_and_validates_versions_options_permissions_and_stale_writes()
    {
        await using var db = new TestDatabase(); await db.InitializeAsync(); var caller = await Owner(db); var service = Service(db);
        var before = (await service.GetMineAsync(caller))!; Assert.Null(before.Settings); Assert.Null(before.Analysis);
        Assert.Null(await service.GetMineAsync(new Caller(null, Guid.NewGuid(), "test", false)));
        Assert.False((await service.AnswerAsync(caller, "music", DiscoveryQuestion.Version, 0, null, false, Guid.Empty)).Succeeded);
        await Configure(service, caller);
        var view = (await service.GetMineAsync(caller))!; var version = view.Settings!.Version;
        Assert.False((await service.AnswerAsync(caller, "music", "unknown-version", 0, null, false, version)).Succeeded);
        Assert.False((await service.AnswerAsync(caller, "music", DiscoveryQuestion.Version, 9, null, false, version)).Succeeded);
        Assert.False((await service.AnswerAsync(caller, "unknown", DiscoveryQuestion.Version, 0, null, false, version)).Succeeded);
        Assert.False((await service.AnswerAsync(caller, "romance-value", DiscoveryQuestion.Version, 0, null, false, version)).Succeeded);
        Assert.False((await service.AnswerAsync(caller, "detail-new", DiscoveryQuestion.Version, 0, null, false, version)).Succeeded);
        await Answer(service, caller, "music", 1);
        Assert.False((await service.AnswerAsync(caller, "music-region", DiscoveryQuestion.Version, 0, null, false, version)).Succeeded);
        var after = (await service.GetMineAsync(caller))!; var answer = Assert.Single(after.Answers);
        Assert.True((await service.AnswerAsync(caller, "music", DiscoveryQuestion.Version, 1, null, false, version)).Succeeded);
        var replay = (await service.GetMineAsync(caller))!;
        Assert.Equal(answer.Revision, Assert.Single(replay.Answers).Revision); Assert.Equal(after.Settings!.Version, replay.Settings!.Version);
        await Answer(service, caller, "music-artist", null, "هنرمند انتخابی من");
        Assert.Contains((await service.GetMineAsync(caller))!.Analysis!.Interests, i => i.Key == "music.artist" && i.Value == "هنرمند انتخابی من");
    }
    [Fact]
    public async Task Sharing_is_explicit_revocable_and_invalidated_when_its_evidence_changes()
    {
        await using var db = new TestDatabase(); await db.InitializeAsync(); var owner = await Owner(db); var other = await Owner(db, "other_owner"); var service = Service(db);
        await Configure(service, owner, relationships: true); await Answer(service, owner, "music");
        var fact = Assert.Single((await service.GetMineAsync(owner))!.Analysis!.Facts); Assert.Null(fact.ShareCode);
        Assert.False((await service.ShareAsync(other, fact.Id, true)).Succeeded);
        var shared = await service.ShareAsync(owner, fact.Id, true); Assert.True(shared.Succeeded); Assert.Equal(24, shared.Value!.Length);
        var publicFact = (await service.GetSharedFactAsync(shared.Value))!; Assert.Empty(publicFact.Fact.Evidence);
        Assert.Equal(fact.Text, publicFact.Fact.Text);
        await Answer(service, owner, "music", 1);
        Assert.Null(await service.GetSharedFactAsync(shared.Value));
        var current = Assert.Single((await service.GetMineAsync(owner))!.Analysis!.Facts);
        var code = (await service.ShareAsync(owner, current.Id, true)).Value!;
        Assert.True((await service.DecideAsync(owner, current.Id, AnalysisDecisionState.Rejected, null)).Succeeded);
        Assert.Null(await service.GetSharedFactAsync(code)); Assert.False((await service.ShareAsync(owner, current.Id, true)).Succeeded);
        await Answer(service, owner, "romance-value");
        var sensitive = (await service.GetMineAsync(owner))!.Analysis!.Facts.Single(f => f.Sensitive);
        Assert.False((await service.ShareAsync(owner, sensitive.Id, true)).Succeeded);
        Assert.True((await service.DecideAsync(owner, current.Id, AnalysisDecisionState.Confirmed, null)).Succeeded);
        code = (await service.ShareAsync(owner, current.Id, true)).Value!;
        await Configure(service, owner);
        Assert.Null(await service.GetSharedFactAsync(code));
        Assert.DoesNotContain((await service.GetMineAsync(owner))!.Answers, a => a.IsSensitive);
    }
    [Fact]
    public async Task Comparison_requires_both_consents_and_uses_only_direct_self_preferences()
    {
        await using var db = new TestDatabase(); await db.InitializeAsync(); var a = await Owner(db); var b = await Owner(db, "comparison_friend"); var service = Service(db);
        await Configure(service, a, true, true); await Configure(service, b);
        foreach (var caller in new[] { a, b })
        {
            await Answer(service, caller, "music", 1); await Answer(service, caller, "music-context");
            await Answer(service, caller, "style-context");
        }
        await Answer(service, a, "romance-value");
        var code = (await service.GetMineAsync(a))!.Settings!.ComparisonCode!;
        Assert.Null(await service.CompareAsync(b, code));
        await Configure(service, b, true);
        var comparison = (await service.CompareAsync(b, code))!;
        Assert.Equal(2, comparison.ComparableCount); Assert.Equal(2, comparison.SameCount);
        Assert.Single(comparison.Facts); Assert.DoesNotContain(comparison.Preferences, i => i.Key.StartsWith("private.") || i.Key == "style.social");
        Assert.Null(await service.CompareAsync(a, code));
        await Configure(service, a, false); Assert.Null(await service.CompareAsync(b, code));
        await Configure(service, a, true); Assert.NotEqual(code, (await service.GetMineAsync(a))!.Settings!.ComparisonCode);
        Assert.Null(await service.CompareAsync(b, code));
    }
    [Fact]
    public async Task Skipping_overrides_game_reference_and_reset_preserves_game_and_disables_analysis()
    {
        await using var db = new TestDatabase(); await db.InitializeAsync(); var caller = await Owner(db); var service = Service(db);
        var games = new PersonalGameService(db, new QuestionFactory(), new InMemoryRateLimiter(TimeProvider.System), TimeProvider.System);
        Assert.True((await games.CreateAsync(caller, GameTests.Answers(), false)).Succeeded);
        await Configure(service, caller);
        Assert.Equal(10, (await service.GetMineAsync(caller))!.Analysis!.Evidence.Count);
        await Answer(service, caller, "music", skip: true);
        Assert.DoesNotContain((await service.GetMineAsync(caller))!.Analysis!.Evidence, e => e.Key == "music");
        Assert.True((await service.ResetAsync(caller)).Succeeded);
        var view = (await service.GetMineAsync(caller))!; Assert.Null(view.Analysis); Assert.Null(view.Settings);
        await using var verify = db.Create();
        Assert.Empty(await verify.DiscoveryAnswers.ToListAsync()); Assert.Empty(await verify.AnalysisDecisions.ToListAsync());
        Assert.Equal(1, await verify.Rooms.CountAsync()); Assert.Equal(10, (await games.GetReferenceAnswersAsync(caller)).Count);
    }
    [Fact]
    public async Task Mbti_correction_is_separate_validated_and_lost_when_evidence_changes()
    {
        await using var db = new TestDatabase(); await db.InitializeAsync(); var caller = await Owner(db); var service = Service(db);
        await Configure(service, caller, mbti: true);
        foreach (var question in DiscoveryCatalog.All.Where(q => q.Dimension is "EI" or "SN" or "TF" or "JP")) await Answer(service, caller, question.Key);
        var mbti = (await service.GetMineAsync(caller))!.Analysis!.Mbti!; Assert.Equal("ESTJ", mbti.Type);
        Assert.False((await service.DecideAsync(caller, mbti.ResultKey, AnalysisDecisionState.Corrected, "XXXX")).Succeeded);
        Assert.True((await service.DecideAsync(caller, mbti.ResultKey, AnalysisDecisionState.Corrected, "infp")).Succeeded);
        var corrected = (await service.GetMineAsync(caller))!.Analysis!.Mbti!; Assert.Equal("INFP", corrected.UserCorrection); Assert.Equal("ESTJ", corrected.Type);
        await Answer(service, caller, "social-weekend", 2);
        Assert.Null((await service.GetMineAsync(caller))!.Analysis!.Mbti!.UserCorrection);
        await Configure(service, caller);
        Assert.Null((await service.GetMineAsync(caller))!.Analysis!.Mbti);
        Assert.DoesNotContain((await service.GetMineAsync(caller))!.Answers, a => DiscoveryCatalog.Find(a.QuestionKey)!.MbtiOnly);
    }
}
