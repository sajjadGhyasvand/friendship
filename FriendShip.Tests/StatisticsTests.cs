using FriendShip.Application.Abstractions;
using FriendShip.Application.Common;
using FriendShip.Application.Questions;
using FriendShip.Application.Services;
using FriendShip.Domain.Participants;
using FriendShip.Domain.Rooms;
using FriendShip.Domain.Users;
using FriendShip.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FriendShip.Tests;

public sealed class StatisticsTests
{
    [Fact]
    public async Task Hundred_friends_equal_ranks_sample_counts_and_single_question_categories()
    {
        await using var database = new TestDatabase(); await database.InitializeAsync();
        var clock = new TestClock(); var limiter = new InMemoryRateLimiter(clock);
        var registered = (await new AuthService(database, new PasswordService(), limiter, clock)
            .RegisterSimpleAsync("stats_owner", "TestPassword!2", "ip")).Value!;
        var owner = new Caller(registered.User.Id, Guid.NewGuid(), "ip", false);
        var games = new PersonalGameService(database, new QuestionFactory(), limiter, clock);
        var code = (await games.CreateAsync(owner, GameTests.Answers(), false)).Value!;
        await using (var db = database.Create())
        {
            var room = await db.Rooms.Include(r => r.Questions).ThenInclude(q => q.Options).SingleAsync(r => r.Code == code);
            for (var i = 0; i < 100; i++)
            {
                var friend = Participant.Join(room, i, Guid.NewGuid(), null, $"دوست {i}", "cat", FriendShip.Domain.Common.ShortCode.Generate(10), clock.Now.UtcDateTime);
                while (!friend.IsCompleted)
                {
                    var next = friend.ServeNext(room, clock.Now.UtcDateTime)!;
                    var question = room.GetQuestion(next.RoomQuestionId);
                    var option = i < 50 ? question.OwnerAnswerOptionId : question.Options.First(o => o.Id != question.OwnerAnswerOptionId).Id;
                    friend.SubmitAnswer(room, question.Id, option, clock.Now.AddSeconds(i + 1).UtcDateTime);
                }
                db.Participants.Add(friend);
            }
            // An unfinished attempt must not affect the average or correctness rates.
            var unfinished = Participant.Join(room, 100, Guid.NewGuid(), null, "بازی نیمه‌کاره", "cat", FriendShip.Domain.Common.ShortCode.Generate(10), clock.Now.UtcDateTime);
            unfinished.ServeNext(room, clock.Now.UtcDateTime);
            db.Participants.Add(unfinished);
            await db.SaveChangesAsync();
        }
        var stats = (await new StatsService(database, clock).GetInsightsAsync(owner, code)).Value!;
        Assert.Equal(101, stats.ParticipantCount); Assert.Equal(100, stats.CompletedCount);
        Assert.Equal(50, stats.AveragePercent); Assert.Equal(100, stats.BestPercent);
        Assert.Equal(1000, stats.CompletedAnswerCount); Assert.Equal(500, stats.CorrectAnswerCount);
        Assert.Equal(50, stats.HighScoreCount);
        Assert.All(stats.Questions, q => { Assert.Equal(100, q.Answers); Assert.Equal(50, q.CorrectRate); });
        Assert.All(stats.Categories.Where(c => c.Answers == 100), c => Assert.Null(c.CorrectRate));
        Assert.All(stats.Categories.Where(c => c.Answers >= 200), c => Assert.Equal(50, c.CorrectRate));
        var results = new ResultsService(database, new AchievementService(database, clock), clock);
        var ranks = (await results.GetLeaderboardAsync(owner, code)).Value!;
        Assert.Equal(100, ranks.Count);
        Assert.All(ranks.Take(50), r => Assert.Equal(1, r.Rank));
        Assert.All(ranks.Skip(50), r => Assert.Equal(51, r.Rank));
        Assert.All(ranks.Take(50), r => { Assert.Equal(10, r.Correct); Assert.Equal(10, r.Total); });
        Assert.All(ranks.Skip(50), r => { Assert.Equal(0, r.Correct); Assert.Equal(10, r.Total); });
        Assert.Equal(ranks, (await results.GetLeaderboardAsync(owner, code)).Value!);
    }

    [Fact]
    public async Task Invalid_or_sparse_reference_data_never_creates_a_game()
    {
        await using var database = new TestDatabase(); await database.InitializeAsync();
        var clock = new TestClock(); var limiter = new InMemoryRateLimiter(clock);
        var registered = (await new AuthService(database, new PasswordService(), limiter, clock)
            .RegisterSimpleAsync("invalid_owner", "TestPassword!3", "ip")).Value!;
        var owner = new Caller(registered.User.Id, Guid.NewGuid(), "ip", false);
        var games = new PersonalGameService(database, new QuestionFactory(), limiter, clock);
        Assert.False((await games.CreateAsync(owner, GameTests.Answers(4), false)).Succeeded);
        var invalid = GameTests.Answers(); invalid["music"] = 99;
        Assert.False((await games.CreateAsync(owner, invalid, false)).Succeeded);
        invalid = GameTests.Answers(); invalid["unknown"] = 0;
        Assert.False((await games.CreateAsync(owner, invalid, false)).Succeeded);
        await using var db = database.Create(); Assert.Equal(0, await db.Rooms.CountAsync());
    }
}
