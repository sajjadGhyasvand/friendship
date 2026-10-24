using FriendShip.Application.Abstractions;
using FriendShip.Application.Common;
using FriendShip.Application.Dtos;
using FriendShip.Domain.Common;
using FriendShip.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace FriendShip.Application.Services;

public sealed class StatsService(
    IAppDbContextFactory dbf,
    TimeProvider time)
{
    private DateTime Now => time.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Owner-only insights. Answer statistics use completed attempts only.
    /// </summary>
    public Task<Result<InsightsDto>> GetInsightsAsync(
        Caller caller,
        string code) =>
        Result<InsightsDto>.RunAsync(async () =>
        {
            if (caller.UserId is not Guid userId)
                return Result<InsightsDto>.Fail("ابتدا وارد شو.");

            await using var db = dbf.Create();

            var normalizedCode = ShortCode.Normalize(code);

            var room = await db.Rooms
                .Include(r => r.Questions)
                .FirstOrDefaultAsync(r =>
                    r.Code == normalizedCode &&
                    r.OwnerUserId == userId);

            if (room is null)
                return Result<InsightsDto>.Fail(
                    "این کوییز پیدا نشد.");

            if (room.ApplyTime(Now))
                await db.SaveChangesAsync();

            if (room.Status == RoomStatus.Cancelled)
                return Result<InsightsDto>.Fail(
                    "آمار کوییز لغوشده قابل نمایش نیست.");

            // A personal game is bounded to 1000 participants and 20 questions.
            // Materializing this bounded data keeps aggregation
            // consistent across SQL Server and SQLite.
            var participants = await db.Participants
                .AsNoTracking()
                .Include(p => p.Answers)
                .Where(p => p.RoomId == room.Id)
                .ToListAsync();

            var completed = participants
                .Where(p => p.IsCompleted)
                .ToList();
            var comparableVersion = room.ScoringVersion == "comparable-v2";
            var scored = completed.Where(p => !comparableVersion || p.Answers.Any(a => a.IsFinal && a.SelectedOptionId.HasValue && !a.TimedOut)).ToList();

            var answersByQuestion = completed
                .SelectMany(p => p.Answers)
                .Where(a => a.IsFinal && (!comparableVersion || a.SelectedOptionId.HasValue && !a.TimedOut))
                .ToLookup(a => a.RoomQuestionId);

            var questionData = room.Questions
                .OrderBy(q => q.Order)
                .Select(q =>
                {
                    var answers = answersByQuestion[q.Id].ToList();

                    return new
                    {
                        q.Order,
                        q.Text,
                        q.CategoryName,
                        q.CategoryIcon,
                        Answers = answers.Count,
                        Correct = answers.Count(a => a.IsCorrect)
                    };
                })
                .ToList();

            var questionStats = questionData
                .Select(q => new QuestionStatDto(
                    q.Order,
                    q.Text,
                    q.Answers,
                    CalculateRate(
                        q.Correct,
                        q.Answers,
                        ResultsService.MinAnswersPerQuestion)))
                .ToList();

            // Room snapshots contain category name/icon, not category ID.
            var categoryStats = questionData
                .GroupBy(q => new
                {
                    q.CategoryName,
                    q.CategoryIcon
                })
                .Select(group =>
                {
                    var answerCount = group.Sum(q => q.Answers);
                    var correctCount = group.Sum(q => q.Correct);

                    return new CategoryStatDto(
                        group.Key.CategoryName,
                        group.Key.CategoryIcon,
                        answerCount,
                        group.Count() >= 2 ? CalculateRate(
                            correctCount,
                            answerCount,
                            ResultsService.MinAnswersPerCategory) : null);
                })
                .ToList();

            var hasEnoughData =
                completed.Count >= ResultsService.MinParticipantsForInsights;

            double? averagePercent =
                scored.Count > 0
                    ? Math.Round(
                        scored.Average(p => p.KnowledgePercent),
                        1,
                        MidpointRounding.AwayFromZero)
                    : null;

            int? bestPercent = scored.Count > 0
                ? scored.Max(p => p.KnowledgePercent)
                : null;

            var eligibleQuestions = questionStats
                .Where(q => q.CorrectRate.HasValue)
                .ToList();

            QuestionStatDto? easiest = null;
            QuestionStatDto? hardest = null;

            if (hasEnoughData && eligibleQuestions.Count > 0)
            {
                easiest = eligibleQuestions
                    .OrderByDescending(q => q.CorrectRate)
                    .ThenBy(q => q.Order)
                    .First();

                hardest = eligibleQuestions
                    .OrderBy(q => q.CorrectRate)
                    .ThenBy(q => q.Order)
                    .First();
            }

            var dto = new InsightsDto(
                participants.Count,
                completed.Count,
                averagePercent,
                bestPercent,
                hasEnoughData,
                questionStats,
                categoryStats,
                easiest,
                hardest,
                completed.Sum(p => comparableVersion ? p.Answers.Count(a => a.IsFinal && a.SelectedOptionId.HasValue && !a.TimedOut) : p.AnsweredCount),
                completed.Sum(p => p.CorrectCount),
                participants.Sum(p => p.AnsweredCount),
                completed.Count(p => p.KnowledgePercent >= 80));

            return Result<InsightsDto>.Ok(dto);
        });

    private static int? CalculateRate(
        int correct,
        int answers,
        int minimumAnswers)
    {
        if (answers < minimumAnswers)
            return null;

        return (int)Math.Round(
            correct * 100.0 / answers,
            MidpointRounding.AwayFromZero);
    }
}
