using FriendShip.Application.Abstractions;
using FriendShip.Application.Common;
using FriendShip.Application.Questions;
using FriendShip.Application.Services;
using FriendShip.Domain.Common;
using FriendShip.Domain.Participants;
using FriendShip.Domain.Questions;
using FriendShip.Domain.Rooms;
using FriendShip.Domain.Scoring;
using FriendShip.Infrastructure.Persistence;
using FriendShip.Infrastructure.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FriendShip.Tests;

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}
public sealed class TestDatabase : IAppDbContextFactory, IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = (AppDbContext)Create();
        await db.Database.EnsureCreatedAsync();
        await FriendShip.Web.Services.QuestionBankSeeder.SeedAsync(this);
    }
    public IAppDbContext Create() => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
public class GameTests
{
    internal static Dictionary<string, int> Answers(int count = 10) => QuestionFactory.Catalog.Take(count).ToDictionary(t => t.Key, _ => 0);
    internal static Room NewRoom(TestClock clock)
    {
        var sources = QuestionFactory.CoreCatalog.Select((t, i) =>
        {
            var source = Question.Create(Category.Create(t.Category, t.Icon, i), t.Prompt, t.Options, clock.Now.UtcDateTime);
            typeof(Question).GetProperty(nameof(Question.Id))!.SetValue(source, i + 1);
            return source;
        }).ToList();
        var room = Room.Create(ShortCode.Generate(24), Guid.NewGuid(), "بازی آزمایشی", RoomSettings.Create(GameMode.Classic, 10, null, 1000), sources, clock.Now.UtcDateTime);
        room.MakePersonal(); new QuestionFactory().Generate(room, Answers()); room.Publish(clock.Now.UtcDateTime); return room;
    }
    [Fact]
    public void Factory_indirect_unique_prompts_and_reference_answer()
    {
        var room = NewRoom(new TestClock());
        Assert.Equal(10, room.Questions.Select(q => q.Text).Distinct().Count());
        Assert.True(room.Questions.Select(q => q.CategoryName).Distinct().Count() >= 3);
        foreach (var q in room.Questions)
        {
            Assert.NotEqual(q.OwnerPrompt, q.Text); Assert.Equal(QuestionFactory.Version, q.GeneratorVersion);
            Assert.Equal(FriendShip.Application.Discovery.DiscoveryQuestion.Version, q.TemplateVersion);
            Assert.NotNull(q.TemplateKey);
            var template = QuestionFactory.Catalog.Single(t => t.Prompt == q.OwnerPrompt);
            Assert.Equal(template.Options[0], q.Options.Single(o => o.Id == q.OwnerAnswerOptionId).Text);
            Assert.Equal(Enumerable.Range(1, q.Options.Count), q.Options.OrderBy(o => o.Order).Select(o => o.Order));
        }
    }
    [Fact]
    public void Correct_choice_moves_across_positions()
    {
        var positions = Enumerable.Range(0, 30).Select(_ => NewRoom(new TestClock()).Questions.First())
            .Select(q => q.Options.Single(o => o.Id == q.OwnerAnswerOptionId).Order).ToHashSet();
        Assert.True(positions.Count > 1);
    }
    [Theory]
    [InlineData(8, 10, 80)] [InlineData(1, 3, 33)] [InlineData(0, 0, 0)] [InlineData(10, 10, 100)]
    public void Percent_calculation(int correct, int total, int expected) => Assert.Equal(expected, ScoreCalculator.Percent(correct, total));
    [Fact]
    public void Resume_duplicate_tampering_pause_and_final_score()
    {
        var clock = new TestClock(); var room = NewRoom(clock);
        var p = Participant.Join(room, 0, Guid.NewGuid(), null, "دوست تست", null, ShortCode.Generate(10), clock.Now.UtcDateTime);
        var first = p.ServeNext(room, clock.Now.UtcDateTime)!;
        Assert.Equal(first.Id, p.ServeNext(room, clock.Now.AddMinutes(2).UtcDateTime)!.Id);
        var q = room.GetQuestion(first.RoomQuestionId);
        Assert.Throws<DomainException>(() => p.SubmitAnswer(room, q.Id, Guid.NewGuid(), clock.Now.UtcDateTime));
        var answer = p.SubmitAnswer(room, q.Id, q.OwnerAnswerOptionId, clock.Now.UtcDateTime);
        Assert.Same(answer, p.SubmitAnswer(room, q.Id, q.OwnerAnswerOptionId, clock.Now.UtcDateTime)); Assert.Single(p.Answers);
        Assert.Throws<DomainException>(() => p.SubmitAnswer(room, q.Id, q.Options.First(o => o.Id != q.OwnerAnswerOptionId).Id, clock.Now.UtcDateTime));
        room.Pause(clock.Now.UtcDateTime); Assert.Throws<DomainException>(() => p.ServeNext(room, clock.Now.UtcDateTime));
        room.Resume(clock.Now.UtcDateTime);
        while (!p.IsCompleted)
        {
            var next = p.ServeNext(room, clock.Now.UtcDateTime)!;
            p.SubmitAnswer(room, next.RoomQuestionId, room.GetQuestion(next.RoomQuestionId).OwnerAnswerOptionId, clock.Now.UtcDateTime);
        }
        Assert.Equal(100, p.KnowledgePercent); Assert.Equal(1000, p.Score);
        Assert.Throws<DomainException>(() => room.SetOwnerAnswer(q.Id, q.Options[0].Id));
    }
    [Fact]
    public void Limiter_preserves_different_windows()
    {
        var clock = new TestClock(); var limiter = new InMemoryRateLimiter(clock);
        Assert.True(limiter.TryAcquire("register", 1, TimeSpan.FromHours(1)));
        clock.Now = clock.Now.AddMinutes(6); Assert.True(limiter.TryAcquire("answer", 10, TimeSpan.FromMinutes(1)));
        Assert.False(limiter.TryAcquire("register", 1, TimeSpan.FromHours(1)));
        clock.Now = clock.Now.AddHours(1); Assert.True(limiter.TryAcquire("register", 1, TimeSpan.FromHours(1)));
    }
    [Fact]
    public async Task Main_flow_persistence_access_stats_rounds_and_deletion()
    {
        await using var dbf = new TestDatabase(); await dbf.InitializeAsync();
        var clock = new TestClock(); var limiter = new InMemoryRateLimiter(clock);
        var auth = new AuthService(dbf, new PasswordService(), limiter, clock);
        var registration = await auth.RegisterSimpleAsync("test_owner", "SamplePass123!", "127.0.0.1"); Assert.True(registration.Succeeded, registration.Error);
        var owner = new Caller(registration.Value!.User.Id, Guid.NewGuid(), "127.0.0.1", false);
        var games = new PersonalGameService(dbf, new QuestionFactory(), limiter, clock);
        var created = await games.CreateAsync(owner, Answers(), false); Assert.True(created.Succeeded, created.Error);
        var info = (await games.GetMineAsync(owner))!;
        Assert.Equal(24, info.LinkCode.Length); Assert.Equal(created.Value, await games.ResolveLinkAsync(info.LinkCode));
        Assert.Equal(created.Value, (await games.CreateAsync(owner, Answers(), false)).Value);
        var stranger = new Caller(null, Guid.NewGuid(), "127.0.0.2", false);
        var rooms = new RoomService(dbf, limiter, clock); var play = new PlayService(dbf, limiter, clock);
        var results = new ResultsService(dbf, new AchievementService(dbf, clock), clock); var stats = new StatsService(dbf, clock);
        Assert.Null(await rooms.GetOwnerViewAsync(stranger, created.Value!));
        Assert.False((await stats.GetInsightsAsync(stranger, created.Value!)).Succeeded);
        var empty = (await stats.GetInsightsAsync(owner, created.Value!)).Value!;
        Assert.Equal(0, empty.CompletedCount); Assert.Null(empty.AveragePercent);
        Assert.True((await play.JoinAsync(stranger, created.Value!, "دوست تست", null)).Succeeded);
        Assert.True((await play.JoinAsync(stranger, created.Value!, "تکراری", null)).Succeeded);
        await using (var check = dbf.Create()) Assert.Equal(1, await check.Participants.CountAsync());
        Assert.False((await results.GetMyResultAsync(stranger, created.Value!)).Succeeded);
        var state = await play.GetStateAsync(stranger, created.Value!);
        Assert.Equal(state.Value!.Question!.RoomQuestionId, (await play.GetStateAsync(stranger, created.Value!)).Value!.Question!.RoomQuestionId);
        var json = System.Text.Json.JsonSerializer.Serialize(state.Value);
        Assert.DoesNotContain("OwnerAnswerOptionId", json); Assert.DoesNotContain("IsCorrect", json);
        var correct = (await rooms.GetOwnerViewAsync(owner, created.Value!))!.Questions.ToDictionary(q => q.Id, q => q.OwnerAnswerOptionId!.Value);
        for (var i = 0; i < 10; i++)
        {
            var q = (await play.GetStateAsync(stranger, created.Value!)).Value!.Question!;
            Assert.True((await play.SubmitAsync(stranger, created.Value!, q.RoomQuestionId, correct[q.RoomQuestionId])).Succeeded);
            Assert.True((await play.SubmitAsync(stranger, created.Value!, q.RoomQuestionId, correct[q.RoomQuestionId])).Succeeded);
        }
        var result = (await results.GetMyResultAsync(stranger, created.Value!)).Value!;
        Assert.Equal(100, result.Percent); Assert.Equal(1000, result.Score); Assert.False(result.AnswersRevealed);
        Assert.All(result.Answers, a => Assert.Null(a.CorrectOptionText));
        var single = (await stats.GetInsightsAsync(owner, created.Value!)).Value!;
        Assert.Equal(100, single.AveragePercent); Assert.Equal(10, single.CompletedAnswerCount); Assert.Null(single.Hardest);
        Assert.NotNull(await results.GetSharedAsync(result.ShareCode));
        clock.Now = clock.Now.AddMinutes(1);
        var next = await games.CreateAsync(owner, Answers(8), true); Assert.True(next.Succeeded, next.Error);
        Assert.NotEqual(created.Value, next.Value); Assert.Equal(next.Value, await games.ResolveLinkAsync(info.LinkCode));
        Assert.Single(await games.GetArchiveAsync(owner)); Assert.NotNull((await results.GetMyResultAsync(stranger, created.Value!)).Value);
        Assert.False((await games.DeleteAsync(stranger, created.Value!)).Succeeded);
        Assert.True((await games.DeleteAsync(owner, created.Value!)).Succeeded); Assert.Null(await results.GetSharedAsync(result.ShareCode));
        Assert.Equal(next.Value, await games.ResolveLinkAsync(info.LinkCode));
        Assert.True((await games.DeleteAsync(owner, next.Value!)).Succeeded); Assert.Null(await games.ResolveLinkAsync(info.LinkCode));
    }
    [Fact]
    public async Task Recovery_one_use_rotates_session_stamp()
    {
        await using var dbf = new TestDatabase(); await dbf.InitializeAsync();
        var clock = new TestClock(); var auth = new AuthService(dbf, new PasswordService(), new InMemoryRateLimiter(clock), clock);
        var registered = (await auth.RegisterSimpleAsync("recover_me", "OldPassword!1", "ip")).Value!;
        var recovered = await auth.RecoverAsync("recover_me", registered.RecoveryCode, "NewPassword!2", "ip"); Assert.True(recovered.Succeeded, recovered.Error);
        Assert.False(await auth.IsSessionValidAsync(registered.User.Id, registered.User.SecurityStamp));
        Assert.False((await auth.RecoverAsync("recover_me", registered.RecoveryCode, "AnotherPass!3", "ip")).Succeeded);
        Assert.True((await auth.LoginAsync("recover_me", "NewPassword!2", "ip")).Succeeded);
    }
}
