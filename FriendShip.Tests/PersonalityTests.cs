using System.Text.Json;
using FriendShip.Application.Abstractions;
using FriendShip.Application.Discovery;
using FriendShip.Application.Services;
using FriendShip.Domain.Discovery;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FriendShip.Tests;

public sealed class PersonalityTests
{
    internal static AnswerEvidence Evidence(string key, int option = 0)
    {
        var q = DiscoveryCatalog.Find(key)!;
        return new(key, q.Prompt, q.Options[option], option, DiscoveryQuestion.Version, "self-discovery", "revision", DateTime.UtcNow, q.Sensitive);
    }

    [Fact]
    public void Narrative_is_bounded_diverse_structured_traceable_and_never_echoes_free_text()
    {
        var all = DiscoveryCatalog.All.Where(q => !q.FreeText).Select(q => Evidence(q.Key)).ToList();
        var q = DiscoveryCatalog.Find("music-artist")!;
        all.Add(new(q.Key, q.Prompt, "PRIVATE_ARTIST_SENTINEL", null, DiscoveryQuestion.Version, "self-discovery", "revision", DateTime.UtcNow, false));
        var analysis = new DiscoveryEngine().Analyze(all, false);
        var publicFacts = analysis.Facts.Where(f => !f.Sensitive).ToArray();
        Assert.InRange(publicFacts.Length, 1, PersonalityComposer.MaxFacts);
        Assert.Equal(publicFacts.Length, publicFacts.DistinctBy(f => f.CategoryKey).Count());
        Assert.All(publicFacts, f => {
            Assert.NotEqual(FactCategory.Other, f.CategoryKey); Assert.NotEmpty(f.VisualKey); Assert.NotEmpty(f.Emoji);
            Assert.NotEmpty(f.RuleKey); Assert.NotEmpty(f.StoryText); Assert.NotEmpty(f.Evidence);
            Assert.DoesNotContain("PRIVATE_ARTIST_SENTINEL", f.Text); Assert.DoesNotContain("انتخابت «", f.Text);
            Assert.All(f.Evidence, e => Assert.Contains(e, analysis.Evidence));
        });
        Assert.DoesNotContain("PRIVATE_ARTIST_SENTINEL", JsonSerializer.Serialize(analysis.Portrait));
        var empty = new DiscoveryEngine().Analyze([], false);
        Assert.Empty(empty.Facts); Assert.Equal("هنوز داریم می‌شناسیمت", empty.Portrait!.Nickname);
    }

    [Fact]
    public void No_social_or_planning_claim_from_insufficient_evidence_and_visuals_match_actual_animals()
    {
        var engine = new DiscoveryEngine();
        var partial = engine.Analyze([Evidence("social-weekend", 2), Evidence("social-party", 2), Evidence("planning", 1)], false);
        Assert.DoesNotContain(partial.Facts, f => f.CategoryKey is FactCategory.Social or FactCategory.Planning);
        Assert.NotEmpty(partial.Portrait!.Unknowns);
        var cat = Assert.Single(engine.Analyze([Evidence("animal", 0)], false).Facts);
        var dog = Assert.Single(engine.Analyze([Evidence("animal", 1)], false).Facts);
        Assert.Equal("animals.cat", cat.VisualKey); Assert.Equal("🐱", cat.Emoji);
        Assert.Equal("animals.dog", dog.VisualKey); Assert.Equal("🐶", dog.Emoji);
        Assert.NotEqual(cat.Id, dog.Id);
        var invalid = Evidence("music") with { Value = "fabricated" };
        Assert.Empty(engine.Analyze([invalid], false).Facts);
    }

    [Fact]
    public void Portrait_combines_real_patterns_and_rejection_cannot_name_the_story()
    {
        var engine = new DiscoveryEngine();
        var evidence = new[] { Evidence("free-time"), Evidence("planning", 1), Evidence("plan-trip", 1), Evidence("plan-project", 1) };
        var analysis = engine.Analyze(evidence, false);
        Assert.Equal("سلطان «یه دست دیگه»", analysis.Portrait!.Nickname);
        var gaming = analysis.Facts.Single(f => f.RuleKey == "gaming.main");
        var decision = AnalysisDecision.Create(Guid.NewGuid(), gaming.Id);
        decision.Set(AnalysisDecisionState.Rejected, null, DateTime.UtcNow);
        Assert.NotEqual(analysis.Portrait.Nickname, engine.Analyze(evidence, false, [decision]).Portrait!.Nickname);
        Assert.Null(PersonalityComposer.Story("من", analysis.Facts));
        var confirmed = analysis.Facts.Select(f => f with { Decision = AnalysisDecisionState.Confirmed }).ToArray();
        var story = PersonalityComposer.Story("من", confirmed, [gaming.Id])!;
        Assert.Single(story.Facts); Assert.Equal("یه دست دیگه!", story.Title);
        Assert.Null(PersonalityComposer.Story("من", confirmed, []));
        Assert.Null(PersonalityComposer.Story("من", confirmed, ["someone-elses-fact"]));
    }

    [Fact]
    public void Story_contains_only_confirmed_non_sensitive_facts_and_no_evidence_or_technical_fields()
    {
        var facts = new DiscoveryEngine().Analyze(DiscoveryCatalog.All.Where(q => !q.FreeText).Select(q => Evidence(q.Key)).ToArray(), false).Facts;
        var confirmed = facts.Select(f => f with { Decision = AnalysisDecisionState.Confirmed }).ToArray();
        var story = PersonalityComposer.Story("صاحب بازی", confirmed)!;
        Assert.InRange(story.Facts.Count, 1, 4);
        var json = JsonSerializer.Serialize(story);
        Assert.DoesNotContain("Evidence", json); Assert.DoesNotContain("OptionIndex", json); Assert.DoesNotContain("Revision", json);
        Assert.All(story.Facts, f => Assert.NotEqual(FactCategory.Other, f.CategoryKey));
        var rejected = confirmed.Select(f => f with { Decision = AnalysisDecisionState.Rejected }).ToArray();
        Assert.Null(PersonalityComposer.Story("من", rejected));
        var privateOnly = confirmed.Select(f => f with { Sensitive = true }).ToArray();
        Assert.Null(PersonalityComposer.Story("من", privateOnly));
    }

    [Fact]
    public async Task Feedback_is_owned_replaceable_idempotent_and_preserves_existing_publication_on_replay()
    {
        await using var database = new TestDatabase(); await database.InitializeAsync();
        var owner = await DiscoveryTests.Owner(database); var other = await DiscoveryTests.Owner(database, "feedback_other");
        var service = new DiscoveryService(database, new DiscoveryEngine(), new AdaptiveQuestionSelector(), new InMemoryRateLimiter(TimeProvider.System), TimeProvider.System);
        Assert.True((await service.ConfigureAsync(owner, true, false, false, false, null)).Succeeded);
        var view = (await service.GetMineAsync(owner))!;
        Assert.True((await service.AnswerAsync(owner, "music", DiscoveryQuestion.Version, 1, null, false, view.Settings!.Version)).Succeeded);
        var fact = Assert.Single((await service.GetMineAsync(owner))!.Analysis!.Facts);
        Assert.False((await service.DecideAsync(other, fact.Id, AnalysisDecisionState.Confirmed, null)).Succeeded);
        Assert.True((await service.DecideAsync(owner, fact.Id, AnalysisDecisionState.Confirmed, null)).Succeeded);
        var code = (await service.ShareAsync(owner, fact.Id, true)).Value!;
        await using var db = database.Create();
        var before = await db.AnalysisDecisions.AsNoTracking().SingleAsync();
        var profileBefore = (await service.GetMineAsync(owner))!.Settings!.Version;
        Assert.True((await service.DecideAsync(owner, fact.Id, AnalysisDecisionState.Confirmed, null)).Succeeded);
        var replay = await db.AnalysisDecisions.AsNoTracking().SingleAsync();
        Assert.Equal(before.Revision, replay.Revision); Assert.Equal(code, replay.ShareCode);
        Assert.Equal(profileBefore, (await service.GetMineAsync(owner))!.Settings!.Version);
        Assert.True((await service.DecideAsync(owner, fact.Id, AnalysisDecisionState.Rejected, null)).Succeeded);
        Assert.Null(await service.GetSharedFactAsync(code));
        Assert.Null(PersonalityComposer.Story("من", (await service.GetMineAsync(owner))!.Analysis!.Facts));
        Assert.True((await service.DecideAsync(owner, fact.Id, AnalysisDecisionState.Confirmed, null)).Succeeded);
        Assert.NotNull(PersonalityComposer.Story("من", (await service.GetMineAsync(owner))!.Analysis!.Facts));
        Assert.Equal(1, await db.AnalysisDecisions.CountAsync());
    }
}
