using FriendShip.Application.Abstractions;
using FriendShip.Application.Common;
using FriendShip.Application.Dtos;
using FriendShip.Domain.Common;
using FriendShip.Domain.Scoring;
using Microsoft.EntityFrameworkCore;

namespace FriendShip.Application.Services;

public sealed class ResultsService(
    IAppDbContextFactory dbf,
    AchievementService achievements,
    TimeProvider time)
{
    public const int MinParticipantsForInsights = 2;
    public const int MinCompletedForAverage = 3;
    public const int MinAnswersPerQuestion = 5;
    public const int MinAnswersPerCategory = 5;
    public const int MinCompletedForPercentile = 5;

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Returns the caller's completed result.
    /// Correct answers are exposed only when allowed by the domain.
    /// </summary>
    public Task<Result<ResultDto>> GetMyResultAsync(
        Caller caller,
        string code) =>
        Result<ResultDto>.RunAsync(async () =>
        {
            await using var db = dbf.Create();

            var normalizedCode = ShortCode.Normalize(code);

            var room = await db.Rooms
                .Include(r => r.Questions)
                .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(r => r.Code == normalizedCode);

            if (room is null)
                return Result<ResultDto>.Fail("این کوییز پیدا نشد.");

            var me = await db.Participants
                .Include(p => p.Answers)
                .FirstOrDefaultAsync(p =>
                    p.RoomId == room.Id &&
                    p.VisitorId == caller.VisitorId);

            if (me is null)
                return Result<ResultDto>.Fail(
                    "هنوز در این کوییز شرکت نکردی.");

            if (!me.IsCompleted)
                return Result<ResultDto>.Fail(
                    "اول کوییز رو تموم کن.");

            if (room.ApplyTime(Now))
                await db.SaveChangesAsync();

            var ownerName = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == room.OwnerUserId)
                .Select(u => u.DisplayName)
                .FirstOrDefaultAsync();

            if (ownerName is null)
                return Result<ResultDto>.Fail(
                    "اطلاعات سازنده‌ی کوییز پیدا نشد.");

            int? rank = null;
            int? participantCount = null;
            int? percentile = null;

            if (room.Settings.ShowLeaderboard)
            {
                var completed = await db.Participants
                    .AsNoTracking()
                    .Where(p =>
                        p.RoomId == room.Id &&
                        p.CompletedAt != null)
                    .Select(p => new
                    {
                        p.Id,
                        p.Score,
                        p.CorrectCount,
                        p.StartedAt,
                        p.CompletedAt
                    })
                    .ToListAsync();

                var ranks = Ranking.Compute(
                    completed.Select(p => new RankInput(
                        p.Id,
                        p.Score,
                        p.CorrectCount,
                        room.Settings.Mode == FriendShip.Domain.Rooms.GameMode.Classic ? TimeSpan.Zero : p.CompletedAt!.Value - p.StartedAt)));

                rank = ranks
                    .FirstOrDefault(r => r.ParticipantId == me.Id)
                    ?.Rank;

                participantCount = completed.Count;

                percentile = Ranking.Percentile(
                    me.Score,
                    completed.Select(p => p.Score).ToList(),
                    MinCompletedForPercentile);
            }

            var revealed = room.AreAnswersRevealable;

            var review = me.Answers
                .Where(a => a.IsFinal)
                .OrderBy(a => a.Order)
                .Select(a =>
                {
                    var question = room.GetQuestion(a.RoomQuestionId);

                    string? yourOption = a.SelectedOptionId is Guid selectedId
                        ? question.Options
                            .FirstOrDefault(o => o.Id == selectedId)?.Text
                        : null;

                    string? correctOption = revealed
                        ? question.Options.FirstOrDefault(
                            o => o.Id == question.OwnerAnswerOptionId)?.Text
                        : null;

                    return new AnswerReviewDto(
                        a.Order,
                        question.Text,
                        question.CategoryIcon,
                        yourOption,
                        correctOption,
                        revealed ? a.IsCorrect : null,
                        a.TimedOut);
                })
                .ToList();

            var tier = TitleCatalog.ForPercent(me.KnowledgePercent);

            // Viewing a guest result does not attach it to a signed-in user.
            if (caller.UserId is Guid userId && me.UserId == userId)
                await achievements.EvaluateAsync(userId);

            var dto = new ResultDto(
                room.Code,
                room.Title,
                ownerName,
                me.DisplayName,
                me.AvatarKey,
                me.KnowledgePercent,
                me.CorrectCount,
                me.TotalQuestions,
                me.Score,
                tier.Title,
                tier.Emoji,
                tier.Message,
                rank,
                participantCount,
                percentile,
                revealed,
                room.IsEnded,
                me.ShareCode,
                review);

            return Result<ResultDto>.Ok(dto);
        });

    /// <summary>
    /// Public share card. Exposes neither answers nor the room code.
    /// </summary>
    public async Task<SharedResultDto?> GetSharedAsync(string shareCode)
    {
        await using var db = dbf.Create();

        var normalizedCode = ShortCode.Normalize(shareCode);

        var row = await (
            from participant in db.Participants.AsNoTracking()
            join room in db.Rooms.AsNoTracking()
                on participant.RoomId equals room.Id
            join owner in db.Users.AsNoTracking()
                on room.OwnerUserId equals owner.Id
            where participant.ShareCode == normalizedCode
                && participant.CompletedAt != null
                && (room.ScoringVersion != "comparable-v2" || participant.Answers.Any(a => a.SelectedOptionId != null && !a.TimedOut))
            select new
            {
                participant.DisplayName,
                participant.AvatarKey,
                participant.KnowledgePercent,
                participant.CorrectCount,
                participant.TotalQuestions,
                Comparable = room.ScoringVersion == "comparable-v2" ? (int?)participant.Answers.Count(a => a.AnsweredAt != null && a.SelectedOptionId != null && !a.TimedOut) : null,
                RoomTitle = room.Title,
                OwnerName = owner.DisplayName
            })
            .FirstOrDefaultAsync();

        if (row is null)
            return null;

        var tier = TitleCatalog.ForPercent(row.KnowledgePercent);

        return new SharedResultDto(
            row.DisplayName,
            row.AvatarKey,
            row.OwnerName,
            row.RoomTitle,
            row.KnowledgePercent,
            tier.Title,
            tier.Emoji,
            tier.Message,
            row.CorrectCount,
            row.TotalQuestions,
            row.Comparable);
    }

    /// <summary>
    /// Always accessible to the owner.
    /// Other callers must be participants and the leaderboard must be enabled.
    /// Only completed attempts are ranked.
    /// </summary>
    public Task<Result<IReadOnlyList<LeaderboardEntryDto>>> GetLeaderboardAsync(
        Caller caller,
        string code) =>
        Result<IReadOnlyList<LeaderboardEntryDto>>.RunAsync(async () =>
        {
            await using var db = dbf.Create();

            var normalizedCode = ShortCode.Normalize(code);

            var room = await db.Rooms
                .FirstOrDefaultAsync(r => r.Code == normalizedCode);

            if (room is null)
                return Result<IReadOnlyList<LeaderboardEntryDto>>.Fail(
                    "این کوییز پیدا نشد.");

            var isOwner = caller.UserId == room.OwnerUserId;

            if (!isOwner)
            {
                var isParticipant = await db.Participants
                    .AsNoTracking()
                    .AnyAsync(p =>
                        p.RoomId == room.Id &&
                        p.VisitorId == caller.VisitorId);

                if (!isParticipant)
                    return Result<IReadOnlyList<LeaderboardEntryDto>>.Fail(
                        "برای دیدن جدول باید در این کوییز شرکت کرده باشی.");

                if (!room.Settings.ShowLeaderboard)
                    return Result<IReadOnlyList<LeaderboardEntryDto>>.Fail(
                        "نمایش جدول رتبه‌بندی این کوییز غیرفعال است.");
            }

            if (room.ApplyTime(Now))
                await db.SaveChangesAsync();

            var completed = await db.Participants
                .AsNoTracking()
                .Where(p =>
                    p.RoomId == room.Id &&
                    p.CompletedAt != null)
                .Select(p => new
                {
                    p.Id,
                    p.VisitorId,
                    p.DisplayName,
                    p.AvatarKey,
                    p.Score,
                    p.KnowledgePercent,
                    p.CorrectCount,
                    p.TotalQuestions,
                    Comparable = room.ScoringVersion == "comparable-v2" ? (int?)p.Answers.Count(a => a.AnsweredAt != null && a.SelectedOptionId != null && !a.TimedOut) : null,
                    p.StartedAt,
                    p.CompletedAt
                })
                .ToListAsync();

            // Stable ordering among exact ties; ranking rules stay unchanged.
            var ranks = Ranking.Compute(
                completed
                    .OrderBy(p => p.Id)
                    .Select(p => new RankInput(
                        p.Id,
                        p.Score,
                        p.CorrectCount,
                        room.Settings.Mode == FriendShip.Domain.Rooms.GameMode.Classic ? TimeSpan.Zero : p.CompletedAt!.Value - p.StartedAt)));

            var byId = completed.ToDictionary(p => p.Id);

            IReadOnlyList<LeaderboardEntryDto> entries = ranks
                .Select(entry =>
                {
                    var participant = byId[entry.ParticipantId];

                    return new LeaderboardEntryDto(
                        entry.Rank,
                        participant.DisplayName,
                        participant.AvatarKey,
                        participant.Score,
                        participant.KnowledgePercent,
                        participant.VisitorId == caller.VisitorId,
                        participant.CorrectCount,
                        participant.TotalQuestions,
                        participant.Comparable);
                })
                .ToList();

            return Result<IReadOnlyList<LeaderboardEntryDto>>.Ok(entries);
        });
}
