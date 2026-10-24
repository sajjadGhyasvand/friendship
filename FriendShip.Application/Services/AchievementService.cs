using FriendShip.Application.Abstractions;
using FriendShip.Application.Common;
using FriendShip.Application.Dtos;
using FriendShip.Domain.Achievements;
using FriendShip.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace FriendShip.Application.Services;

public sealed class AchievementService(
    IAppDbContextFactory dbf,
    TimeProvider time)
{
    public static readonly IReadOnlyDictionary<
        string,
        (string Title, string Description, string Emoji)> Catalog =
        new Dictionary<
            string,
            (string Title, string Description, string Emoji)>
        {
            [AchievementKeys.FirstQuiz] = (
                "اولین کوییز",
                "اولین کوییز دوستت رو کامل کردی.",
                "🎉"),

            [AchievementKeys.PerfectScore] = (
                "صددرصد!",
                "یه کوییز رو با نمره‌ی کامل تموم کردی.",
                "💯"),

            [AchievementKeys.FiveRooms] = (
                "میزبان حرفه‌ای",
                "پنج کوییز ساختی.",
                "🏠"),

            [AchievementKeys.TenFriends] = (
                "دوست‌دار",
                "ده نفر کوییز تو رو کامل کردن.",
                "👯")
        };

    /// <summary>
    /// Unlocks newly earned achievements.
    /// Cancelled rooms are excluded; previously unlocked awards remain.
    /// Requires a unique database index on (UserId, Key).
    /// </summary>
    public async Task EvaluateAsync(Guid userId)
    {
        if (userId == Guid.Empty)
            return;

        await using var db = dbf.Create();

        var eligibleUser = await db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId && !u.IsBanned);

        if (!eligibleUser)
            return;

        var existingKeys = await db.Achievements
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => a.Key)
            .ToListAsync();

        var have = existingKeys.ToHashSet(StringComparer.Ordinal);
        var toAdd = new List<string>();

        if (!have.Contains(AchievementKeys.FirstQuiz) ||
            !have.Contains(AchievementKeys.PerfectScore))
        {
            var completed =
                from participant in db.Participants.AsNoTracking()
                join room in db.Rooms.AsNoTracking()
                    on participant.RoomId equals room.Id
                where participant.UserId == userId
                    && participant.CompletedAt != null
                    && room.Status != RoomStatus.Cancelled
                select participant;

            if (!have.Contains(AchievementKeys.FirstQuiz) &&
                await completed.AnyAsync())
            {
                toAdd.Add(AchievementKeys.FirstQuiz);
            }

            if (!have.Contains(AchievementKeys.PerfectScore) &&
                await completed.AnyAsync(p => p.KnowledgePercent == 100))
            {
                toAdd.Add(AchievementKeys.PerfectScore);
            }
        }

        if (!have.Contains(AchievementKeys.FiveRooms))
        {
            var roomCount = await db.Rooms
                .AsNoTracking()
                .CountAsync(r =>
                    r.OwnerUserId == userId &&
                    r.Status != RoomStatus.Cancelled);

            if (roomCount >= 5)
                toAdd.Add(AchievementKeys.FiveRooms);
        }

        if (!have.Contains(AchievementKeys.TenFriends))
        {
            var friendCount = await (
                from participant in db.Participants.AsNoTracking()
                join room in db.Rooms.AsNoTracking()
                    on participant.RoomId equals room.Id
                where room.OwnerUserId == userId
                    && room.Status != RoomStatus.Cancelled
                    && participant.CompletedAt != null
                select participant.VisitorId)
                .Distinct()
                .CountAsync();

            if (friendCount >= 10)
                toAdd.Add(AchievementKeys.TenFriends);
        }

        if (toAdd.Count == 0)
            return;

        var now = time.GetUtcNow().UtcDateTime;

        foreach (var key in toAdd)
            db.Achievements.Add(Achievement.Unlock(userId, key, now));

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Another operation may have unlocked the same awards.
            // Verify the desired state instead of swallowing every error.
            await using var verificationDb = dbf.Create();

            var persistedKeys = await verificationDb.Achievements
                .AsNoTracking()
                .Where(a =>
                    a.UserId == userId &&
                    toAdd.Contains(a.Key))
                .Select(a => a.Key)
                .ToListAsync();

            var persisted = persistedKeys
                .ToHashSet(StringComparer.Ordinal);

            if (!toAdd.All(key => persisted.Contains(key)))
                throw;
        }
    }

    /// <summary>
    /// Evaluates and returns only the signed-in caller's achievements.
    /// </summary>
    public Task<Result<IReadOnlyList<AchievementDto>>> GetMineAsync(
        Caller caller) =>
        Result<IReadOnlyList<AchievementDto>>.RunAsync(async () =>
        {
            if (caller.UserId is not Guid userId)
            {
                return Result<IReadOnlyList<AchievementDto>>.Fail(
                    "برای دیدن نشان‌ها وارد شو.");
            }

            await EvaluateAsync(userId);

            await using var db = dbf.Create();

            var rows = await db.Achievements
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.UnlockedAt)
                .ThenBy(a => a.Key)
                .ToListAsync();

            IReadOnlyList<AchievementDto> items = rows
                .Select(a =>
                {
                    if (Catalog.TryGetValue(a.Key, out var info))
                    {
                        return new AchievementDto(
                            a.Key,
                            info.Title,
                            info.Description,
                            info.Emoji,
                            a.UnlockedAt);
                    }

                    return new AchievementDto(
                        a.Key,
                        "نشان",
                        "این نشان قبلاً دریافت شده است.",
                        "🏅",
                        a.UnlockedAt);
                })
                .ToList();

            return Result<IReadOnlyList<AchievementDto>>.Ok(items);
        });
}
