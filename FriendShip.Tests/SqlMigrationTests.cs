using FriendShip.Application.Abstractions;
using FriendShip.Application.Common;
using FriendShip.Application.Questions;
using FriendShip.Application.Services;
using FriendShip.Domain.Users;
using FriendShip.Infrastructure.Persistence;
using FriendShip.Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace FriendShip.Tests;

public sealed class SqlMigrationTests
{
    private sealed class SqlFactory(string connection) : IAppDbContextFactory
    {
        public IAppDbContext Create() => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options);
    }
    [Fact, Trait("Category", "SqlServer")]
    public async Task Migration_preserves_legacy_data_and_supports_personal_rounds()
    {
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("FRIENDSHIP_TEST_SQL") ??
            "Server=.;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5")
        { InitialCatalog = "FriendShipTests_" + Guid.NewGuid().ToString("N") };
        Assert.StartsWith("FriendShipTests_", builder.InitialCatalog);
        var factory = new SqlFactory(builder.ConnectionString);
        await using var db = (AppDbContext)factory.Create();
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261012072050_mig-init");
            var legacyId = Guid.NewGuid(); var oldRoomId = Guid.NewGuid(); var now = DateTime.UtcNow;
            var oldParticipantId = Guid.NewGuid();
            var hasher = new PasswordService(); var legacyHash = hasher.Hash("LegacyPassword!1");
            await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO Users (Id,PublicId,Email,DisplayName,PasswordHash,Role,AvatarKey,IsProfilePublic,IsBanned,SecurityStamp,CreatedAt)
                VALUES ({legacyId},{"23456789AB"},{"legacy@example.test"},{"کاربر قبلی"},{legacyHash},0,{"cat"},0,0,{Guid.NewGuid().ToString("N")},{now});
                INSERT INTO Rooms (Id,Code,OwnerUserId,Title,Mode,QuestionCount,MaxParticipants,RevealMode,ShowLeaderboard,Status,CreatedAt,ExpiresAt,Version)
                VALUES ({oldRoomId},{"234567"},{legacyId},{"بازی قبلی"},0,5,20,0,1,0,{now},{now.AddDays(7)},{Guid.NewGuid()});
                INSERT INTO Participants (Id,RoomId,VisitorId,DisplayName,AvatarKey,ShareCode,StartedAt,CompletedAt,TotalQuestions,CorrectCount,Score,KnowledgePercent)
                VALUES ({oldParticipantId},{oldRoomId},{Guid.NewGuid()},{"دوست قدیمی"},{"cat"},{"23456789AB"},{now},{now},5,1,100,20);");
            await db.GetService<IMigrator>().MigrateAsync("20261021133911_DiscoveryAnalysis");
            var decisionRevision = Guid.NewGuid();
            // Seed the previous schema explicitly: the new CLR model already contains the additive columns.
            await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO DiscoveryProfiles
                (UserId,Enabled,AllowComparison,AllowRelationships,AllowMbti,Version,UpdatedAt)
                VALUES ({legacyId},1,0,0,0,{Guid.NewGuid()},{now});
                INSERT INTO AnalysisDecisions (UserId,ResultKey,State,UpdatedAt,Revision)
                VALUES ({legacyId},{"old-fact-key"},1,{now},{decisionRevision});");
            await db.Database.MigrateAsync();
            Assert.Equal(4, (await db.Database.GetAppliedMigrationsAsync()).Count());
            var oldSettings = await db.DiscoveryProfiles.AsNoTracking().SingleAsync();
            Assert.True(oldSettings.Enabled); Assert.False(oldSettings.AllowAggregateSharing);
            var oldVote = await db.AnalysisDecisions.AsNoTracking().SingleAsync();
            Assert.Equal(decisionRevision, oldVote.Revision);
            Assert.Equal(FriendShip.Domain.Discovery.AnalysisDecisionState.Confirmed, oldVote.State);
            Assert.Null(oldVote.RuleKey); Assert.Null(oldVote.CopyVersion); Assert.Null(oldVote.EvidenceCount); Assert.Null(oldVote.FeedbackAt);
            var old = await db.Rooms.AsNoTracking().SingleAsync(); Assert.Equal("234567", old.Code); Assert.Equal(oldRoomId, old.Id);
            var clock = new TestClock(); var limiter = new InMemoryRateLimiter(clock);
            var auth = new AuthService(factory, hasher, limiter, clock);
            Assert.True((await auth.LoginAsync("legacy@example.test", "LegacyPassword!1", "test")).Succeeded);
            await FriendShip.Web.Services.QuestionBankSeeder.SeedAsync(factory);
            var games = new PersonalGameService(factory, new QuestionFactory(), limiter, clock);
            var owner = new Caller(legacyId, Guid.NewGuid(), "test", false);
            var discovery = new DiscoveryService(factory, new FriendShip.Application.Discovery.DiscoveryEngine(),
                new FriendShip.Application.Discovery.AdaptiveQuestionSelector(), limiter, clock);
            Assert.True((await discovery.ConfigureAsync(owner, true, false, false, false, oldSettings.Version)).Succeeded);
            var profile = (await discovery.GetMineAsync(owner))!;
            Assert.True((await discovery.AnswerAsync(owner, "music", FriendShip.Application.Discovery.DiscoveryQuestion.Version,
                1, null, false, profile.Settings!.Version)).Succeeded);
            Assert.Single((await discovery.GetMineAsync(owner))!.Analysis!.Interests);
            Assert.Null(old.ScoringVersion);
            var preserved = await db.Participants.AsNoTracking().SingleAsync(p => p.Id == oldParticipantId);
            Assert.Equal(20, preserved.KnowledgePercent); Assert.Equal(100, preserved.Score); Assert.Equal(1, preserved.CorrectCount);
            var game = await games.CreateAsync(owner, GameTests.Answers(), false); Assert.True(game.Succeeded, game.Error);
            var before = (await games.GetMineAsync(owner))!;
            var next = await games.CreateAsync(owner, GameTests.Answers(8), true); Assert.True(next.Succeeded, next.Error);
            Assert.Equal(next.Value, await games.ResolveLinkAsync(before.LinkCode));
            Assert.Equal(2, (await games.GetArchiveAsync(owner)).Count);
            Assert.True((await games.DeleteAsync(owner, next.Value!)).Succeeded);
            Assert.NotNull(await db.Rooms.AsNoTracking().SingleOrDefaultAsync(r => r.Id == oldRoomId));
        }
        finally
        {
            // The generated database is owned by this test. Never use the configured application database.
            Assert.StartsWith("FriendShipTests_", builder.InitialCatalog);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
