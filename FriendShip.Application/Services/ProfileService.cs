using FriendShip.Application.Abstractions;
using FriendShip.Application.Common;
using FriendShip.Application.Dtos;
using FriendShip.Domain.Common;
using FriendShip.Domain.Rooms;
using FriendShip.Domain.Scoring;
using FriendShip.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FriendShip.Application.Services;

public sealed class ProfileService(
    IAppDbContextFactory dbf,
    AchievementService achievements,
    TimeProvider time)
{
    public const int RecentItemsLimit = 50;

    public IReadOnlyList<AvatarInfo> GetAvatars() => AvatarCatalog.All;

    public Task<Result<ProfileDto>> GetMineAsync(Caller caller)
    {
        if (caller.UserId is null)
        {
            return Task.FromResult(
                Result<ProfileDto>.Fail("برای دیدن پروفایل وارد شو."));
        }

        return LoadAsync(caller, publicId: null);
    }

    public Task<Result<ProfileDto>> GetPublicAsync(
        Caller caller,
        string publicId)
    {
        var normalized = ShortCode.Normalize(publicId);

        if (!ShortCode.IsValid(normalized, AppUser.PublicIdLength))
        {
            return Task.FromResult(
                Result<ProfileDto>.Fail("این پروفایل در دسترس نیست."));
        }

        return LoadAsync(caller, normalized);
    }

    public Task<Result> UpdateAsync(
        Caller caller,
        string displayName,
        string? avatarKey,
        string? bio,
        bool isPublic) =>
        Result.RunAsync(async () =>
        {
            if (caller.UserId is not Guid userId)
                return Result.Fail("ابتدا وارد شو.");

            await using var db = dbf.Create();

            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null || user.IsBanned)
                return Result.Fail("حساب کاربری در دسترس نیست.");

            // Domain performs name, avatar and biography validation.
            user.UpdateProfile(displayName, avatarKey, bio, isPublic);

            await db.SaveChangesAsync();

            return Result.Ok();
        });

    private Task<Result<ProfileDto>> LoadAsync(
        Caller caller,
        string? publicId) =>
        Result<ProfileDto>.RunAsync(async () =>
        {
            await using var db = dbf.Create();

            var userQuery = db.Users.AsNoTracking();

            var user = publicId is null
                ? await userQuery.FirstOrDefaultAsync(
                    u => u.Id == caller.UserId)
                : await userQuery.FirstOrDefaultAsync(
                    u => u.PublicId == publicId);

            if (user is null || user.IsBanned)
            {
                return Result<ProfileDto>.Fail(
                    "این پروفایل در دسترس نیست.");
            }

            var isOwnProfile = caller.UserId == user.Id;

            if (!isOwnProfile && !user.IsProfilePublic)
            {
                return Result<ProfileDto>.Fail(
                    "این پروفایل در دسترس نیست.");
            }

            // Reading somebody else's public profile must not mutate it.
            if (isOwnProfile)
                await achievements.EvaluateAsync(user.Id);

            var roomsCreated = await db.Rooms
                .AsNoTracking()
                .CountAsync(r =>
                    r.OwnerUserId == user.Id &&
                    r.Status != RoomStatus.Cancelled);

            var playedQuery =
                from participant in db.Participants.AsNoTracking()
                join room in db.Rooms.AsNoTracking()
                    on participant.RoomId equals room.Id
                where participant.UserId == user.Id
                    && participant.CompletedAt != null
                    && room.Status != RoomStatus.Cancelled
                    && (room.ScoringVersion != "comparable-v2" || participant.Answers.Any(a => a.SelectedOptionId != null && !a.TimedOut))
                select new
                {
                    participant.Id,
                    participant.KnowledgePercent,
                    participant.ShareCode,
                    participant.CompletedAt,
                    RoomCode = room.Code,
                    RoomTitle = room.Title,
                    room.OwnerUserId
                };

            var quizzesPlayed = await playedQuery.CountAsync();

            double? averagePercent = null;

            if (quizzesPlayed >= ResultsService.MinCompletedForAverage)
            {
                var average = await playedQuery
                    .AverageAsync(p => (double)p.KnowledgePercent);

                averagePercent = Math.Round(
                    average,
                    1,
                    MidpointRounding.AwayFromZero);
            }

            int? bestPercent = quizzesPlayed > 0
                ? await playedQuery.MaxAsync(p => p.KnowledgePercent)
                : null;

            var achievementRows = await db.Achievements
                .AsNoTracking()
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.UnlockedAt)
                .ThenBy(a => a.Key)
                .ToListAsync();

            var achievementDtos = achievementRows
                .Select(a =>
                {
                    if (AchievementService.Catalog.TryGetValue(
                        a.Key, out var info))
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

            IReadOnlyList<PlayedQuizDto> history =
                Array.Empty<PlayedQuizDto>();

            IReadOnlyList<CreatedRoomDto> created =
                Array.Empty<CreatedRoomDto>();

            if (isOwnProfile)
            {
                var recentPlayed = await (
                    from played in playedQuery
                    join owner in db.Users.AsNoTracking()
                        on played.OwnerUserId equals owner.Id
                    orderby played.CompletedAt descending, played.Id
                    select new
                    {
                        played.RoomCode,
                        played.RoomTitle,
                        OwnerName = owner.DisplayName,
                        played.KnowledgePercent,
                        played.ShareCode,
                        played.CompletedAt
                    })
                    .Take(RecentItemsLimit)
                    .ToListAsync();

                history = recentPlayed
                    .Select(p =>
                    {
                        var tier = TitleCatalog.ForPercent(
                            p.KnowledgePercent);

                        return new PlayedQuizDto(
                            p.RoomCode,
                            p.RoomTitle,
                            p.OwnerName,
                            p.KnowledgePercent,
                            tier.Title,
                            tier.Emoji,
                            p.ShareCode,
                            p.CompletedAt!.Value);
                    })
                    .ToList();

                // Load tracked rooms so lazy expiration can be persisted.
                var recentRooms = await db.Rooms
                    .Where(r => r.OwnerUserId == user.Id)
                    .OrderByDescending(r => r.CreatedAt)
                    .ThenBy(r => r.Id)
                    .Take(RecentItemsLimit)
                    .ToListAsync();
                var now = time.GetUtcNow().UtcDateTime;
                var changed = false;

                foreach (var room in recentRooms)
                {
                    if (room.ApplyTime(now))
                        changed = true;
                }

                if (changed)
                    await db.SaveChangesAsync();
                var roomIds = recentRooms
                    .Select(r => r.Id)
                    .ToList();

                var participantCounts = new Dictionary<Guid, int>();

                if (roomIds.Count > 0)
                {
                    participantCounts = await db.Participants
                        .AsNoTracking()
                        .Where(p => roomIds.Contains(p.RoomId))
                        .GroupBy(p => p.RoomId)
                        .Select(group => new
                        {
                            RoomId = group.Key,
                            Count = group.Count()
                        })
                        .ToDictionaryAsync(x => x.RoomId, x => x.Count);
                }

                created = recentRooms
                    .Select(r => new CreatedRoomDto(
                        r.Code,
                        r.Title,
                        r.Status,
                        participantCounts.GetValueOrDefault(r.Id),
                        r.CreatedAt))
                    .ToList();
            }

            return Result<ProfileDto>.Ok(new ProfileDto(
                user.PublicId,
                user.DisplayName,
                user.AvatarKey,
                user.Bio,
                user.IsProfilePublic,
                isOwnProfile && user.UserName is null ? user.Email : null,
                user.CreatedAt,
                roomsCreated,
                quizzesPlayed,
                averagePercent,
                bestPercent,
                achievementDtos,
                  history,
                created,
                isOwnProfile));
        });
}
