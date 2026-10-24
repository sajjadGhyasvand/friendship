using FriendShip.Application.Common;
using FriendShip.Application.Abstractions;
using FriendShip.Application.Discovery;
using FriendShip.Application.Questions;
using FriendShip.Application.Services;
using FriendShip.Domain.Common;
using FriendShip.Domain.Participants;
using FriendShip.Domain.Rooms;
using Xunit;

namespace FriendShip.Tests;

public sealed class KnowledgeTests
{
    [Fact]
    public void Aggregate_story_requires_five_distinct_completed_players_and_never_contains_individual_details()
    {
        var clock = new TestClock(); var room = GameTests.NewRoom(clock); var engine = new KnowledgeEngine();
        var peers = Enumerable.Range(0, 5).Select(i => Complete(room, clock, i + 2, 8 - i, "PRIVATE_NAME_" + i)).ToArray();
        Assert.Null(engine.Story(room, peers.Take(4).ToArray(), "من"));
        Assert.Null(engine.Story(room, [peers[0], peers[0], peers[0], peers[0], peers[0]], "من"));
        var story = engine.Story(room, peers, "من")!;
        Assert.Equal(5, story.SampleCount); Assert.Equal(40, story.Percent);
        Assert.InRange(story.Facts.Count, 2, 3);
        var json = System.Text.Json.JsonSerializer.Serialize(story, new System.Text.Json.JsonSerializerOptions {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        foreach (var p in peers) { Assert.DoesNotContain(p.DisplayName, json); Assert.DoesNotContain(p.Id.ToString(), json); }
        foreach (var q in room.Questions)
        {
            Assert.DoesNotContain(q.Text, json);
            foreach (var option in q.Options) Assert.DoesNotContain("\"" + option.Text + "\"", json);
        }
        Assert.DoesNotContain("SelectedOptionId", json); Assert.DoesNotContain("RoomQuestionId", json);
        Assert.DoesNotContain("همه دوستات", json);
        var other = GameTests.NewRoom(clock);
        Assert.Null(engine.Story(other, peers, "من"));
    }

    [Fact]
    public void Story_excludes_skips_unknown_templates_and_sensitive_snapshots()
    {
        var clock = new TestClock(); var room = GameTests.NewRoom(clock); var engine = new KnowledgeEngine();
        var skipped = Enumerable.Range(0, 5).Select(_ => Complete(room, clock, 0, 0)).ToArray();
        Assert.Null(engine.Story(room, skipped, "من"));
        var few = Enumerable.Range(0, 5).Select(_ => Complete(room, clock, 1, 0)).ToArray();
        Assert.Null(engine.Story(room, few, "من"));
        var complete = Enumerable.Range(0, 5).Select(_ => Complete(room, clock, 10, 0)).ToArray();
        foreach (var q in room.Questions) typeof(RoomQuestion).GetProperty(nameof(RoomQuestion.TemplateKey))!.SetValue(q, "unknown-private");
        Assert.Null(engine.Story(room, complete, "من"));
        foreach (var q in room.Questions) typeof(RoomQuestion).GetProperty(nameof(RoomQuestion.TemplateKey))!.SetValue(q, "romance-value");
        Assert.Null(engine.Story(room, complete, "من"));
    }
    private static Participant Complete(Room room, TestClock clock, int correct, int wrong, string name = "دوست آزمون")
    {
        var p = Participant.Join(room, 0, Guid.NewGuid(), null, name, null, ShortCode.Generate(10), clock.Now.UtcDateTime);
        while (!p.IsCompleted)
        {
            var a = p.ServeNext(room, clock.Now.UtcDateTime)!; var question = room.GetQuestion(a.RoomQuestionId);
            Guid? option = p.AnsweredCount < correct ? question.OwnerAnswerOptionId :
                p.AnsweredCount < correct + wrong ? question.Options.First(o => o.Id != question.OwnerAnswerOptionId).Id : null;
            p.SubmitAnswer(room, question.Id, option, clock.Now.UtcDateTime);
        }
        return p;
    }
    [Fact]
    public void Null_answers_are_excluded_and_category_minimum_and_total_formula_are_explicit()
    {
        var clock = new TestClock(); var room = GameTests.NewRoom(clock); var p = Complete(room, clock, 2, 1);
        var analysis = new KnowledgeEngine().Analyze(room, p.Answers);
        Assert.Equal(67, p.KnowledgePercent); Assert.Equal(200, p.Score);
        Assert.Equal(67, analysis.Percent); Assert.Equal(3, analysis.Comparable); Assert.Equal(7, analysis.Excluded);
        Assert.Equal(2, analysis.Correct);
        Assert.Equal(analysis.Comparable, analysis.Categories.Sum(c => c.Comparable));
        Assert.All(analysis.Categories.Where(c => c.Comparable < 2), c => Assert.Null(c.Percent));
        Assert.All(analysis.Categories.Where(c => c.Comparable >= 2), c => Assert.NotNull(c.Percent));
        var empty = new KnowledgeEngine().Analyze(room, Complete(room, clock, 0, 0).Answers);
        Assert.Null(empty.Percent); Assert.Equal(0, empty.Comparable); Assert.Null(empty.BestCategory);
    }
    [Fact]
    public void Legacy_scoring_is_preserved_while_comparable_analysis_is_available()
    {
        var clock = new TestClock(); var room = GameTests.NewRoom(clock);
        // Emulate a pre-migration snapshot. The nullable new column is not backfilled.
        typeof(Room).GetProperty(nameof(Room.ScoringVersion))!.SetValue(room, null);
        var p = Complete(room, clock, 2, 1);
        Assert.Equal(20, p.KnowledgePercent);
        Assert.Equal(67, new KnowledgeEngine().Analyze(room, p.Answers).Percent);
        Assert.Equal(20, p.KnowledgePercent);
    }
    [Fact]
    public void Owner_facts_need_peers_and_frequency_needs_five_and_ties_remain_ties()
    {
        var clock = new TestClock(); var room = GameTests.NewRoom(clock); var engine = new KnowledgeEngine();
        var first = Complete(room, clock, 10, 0, "دوست اول");
        Assert.Empty(engine.OwnerFacts(room, [first]));
        var second = Complete(room, clock, 10, 0, "دوست دوم");
        var pair = engine.OwnerFacts(room, [first, second]);
        var leaders = Assert.Single(pair, f => f.Id == "overall-leaders");
        Assert.Contains("دوست اول", leaders.Text); Assert.Contains("دوست دوم", leaders.Text);
        Assert.DoesNotContain(pair, f => f.Id == "unexpected-question");
        var five = engine.OwnerFacts(room, [first, second, Complete(room, clock, 0, 10), Complete(room, clock, 0, 10), Complete(room, clock, 0, 10)]);
        Assert.Contains(five, f => f.Id == "unexpected-question" && f.SampleCount == 5);
        Assert.Contains(five, f => f.Id == "popular-guess" && f.Text.Contains("نه علاقه"));
        var partial = Complete(room, clock, 1, 0);
        Assert.DoesNotContain(engine.OwnerFacts(room, [first, partial]), f => f.Id == "overall-leaders");
    }
    [Fact]
    public async Task All_skipped_is_complete_but_has_no_public_percentage_or_average_and_owner_access_is_checked()
    {
        await using var db = new TestDatabase(); await db.InitializeAsync(); var owner = await DiscoveryTests.Owner(db);
        var clock = new TestClock(); var limiter = new InMemoryRateLimiter(clock);
        var games = new PersonalGameService(db, new QuestionFactory(), limiter, clock);
        var code = (await games.CreateAsync(owner, GameTests.Answers(), false)).Value!;
        var friend = new Caller(null, Guid.NewGuid(), "test", false); var play = new PlayService(db, limiter, clock);
        Assert.True((await play.JoinAsync(friend, code, "دوست مردد", null)).Succeeded);
        var knowledge = new FriendKnowledgeService(db, new KnowledgeEngine());
        Assert.Null(await knowledge.GetMineAsync(friend, code));
        string shareCode = "";
        for (var i = 0; i < 10; i++)
        {
            var state = (await play.GetStateAsync(friend, code)).Value!; shareCode = state.ShareCode;
            Assert.True(state.Question!.CanSkip);
            Assert.True((await play.SubmitAsync(friend, code, state.Question.RoomQuestionId, null)).Succeeded);
        }
        Assert.Null((await knowledge.GetMineAsync(friend, code))!.Percent);
        Assert.Null(await knowledge.GetMineAsync(new Caller(null, Guid.NewGuid(), "test", false), code));
        Assert.Empty(await knowledge.GetOwnerFactsAsync(friend, code));
        Assert.Empty(await knowledge.GetOwnerFactsAsync(new Caller(Guid.NewGuid(), Guid.NewGuid(), "test", false), code));
        Assert.Null(await knowledge.GetOwnerStoryAsync(friend, code, "مالک جعلی"));
        Assert.Null(await knowledge.GetOwnerStoryAsync(new Caller(Guid.NewGuid(), Guid.NewGuid(), "test", false), code, "مالک جعلی"));
        var stats = (await new StatsService(db, clock).GetInsightsAsync(owner, code)).Value!;
        Assert.Equal(1, stats.CompletedCount); Assert.Null(stats.AveragePercent); Assert.Null(stats.BestPercent); Assert.Equal(0, stats.CompletedAnswerCount);
        Assert.Null(await new ResultsService(db, new AchievementService(db, clock), clock).GetSharedAsync(shareCode));
    }
}
