using FriendShip.Domain.Moderation;
using FriendShip.Domain.Rooms;
using FriendShip.Domain.Users;

namespace FriendShip.Application.Dtos;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int Pages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

/// <summary>CorrectOptionText/IsCorrect are null unless the room allows revealing answers.</summary>
public sealed record AnswerReviewDto(int Order, string QuestionText, string CategoryIcon,
    string? YourOptionText, string? CorrectOptionText, bool? IsCorrect, bool TimedOut);

public sealed record ResultDto(string RoomCode, string RoomTitle, string OwnerName, string ParticipantName,
    string AvatarKey, int Percent, int Correct, int Total, int Score,
    string TierTitle, string TierEmoji, string TierMessage,
    int? Rank, int? ParticipantCount, int? Percentile,
    bool AnswersRevealed, bool RoomEnded, string ShareCode,
    IReadOnlyList<AnswerReviewDto> Answers);

/// <summary>Public share card: no answers, no room code.</summary>
public sealed record SharedResultDto(string ParticipantName, string AvatarKey, string OwnerName, string RoomTitle,
    int Percent, string TierTitle, string TierEmoji, string TierMessage, int Correct, int Total, int? Comparable);

public sealed record LeaderboardEntryDto(int Rank, string Name, string AvatarKey, int Score, int Percent, bool IsMe,
    int Correct, int Total, int? Comparable = null);

public sealed record QuestionStatDto(int Order, string Text, int Answers, int? CorrectRate);
public sealed record CategoryStatDto(string Name, string Icon, int Answers, int? CorrectRate);

public sealed record InsightsDto(int ParticipantCount, int CompletedCount, double? AveragePercent,
    int? BestPercent, bool HasEnoughData,
    IReadOnlyList<QuestionStatDto> Questions, IReadOnlyList<CategoryStatDto> Categories,
    QuestionStatDto? Easiest, QuestionStatDto? Hardest,
    int CompletedAnswerCount, int CorrectAnswerCount, int RegisteredAnswerCount, int HighScoreCount);

public sealed record AchievementDto(string Key, string Title, string Description, string Emoji, DateTime UnlockedAt);

public sealed record PlayedQuizDto(string RoomCode, string RoomTitle, string OwnerName, int Percent,
    string TierTitle, string TierEmoji, string ShareCode, DateTime CompletedAt);

public sealed record CreatedRoomDto(string Code, string Title, RoomStatus Status, int ParticipantCount, DateTime CreatedAt);

public sealed record ProfileDto(string PublicId, string DisplayName, string AvatarKey, string? Bio,
    bool IsPublic, string? Email, DateTime JoinedAt, int RoomsCreated, int QuizzesPlayed,
    double? AveragePercent, int? BestPercent, IReadOnlyList<AchievementDto> Achievements,
    IReadOnlyList<PlayedQuizDto> History, IReadOnlyList<CreatedRoomDto> Created, bool IsOwnProfile);

public sealed record AdminDashboardDto(int Users, int Rooms, int OpenRooms, int Participants,
    int PendingReports, int ErrorsLast24h);

public sealed record AdminUserDto(Guid Id, string DisplayName, string Email, UserRole Role,
    bool IsBanned, string? BanReason, DateTime CreatedAt, DateTime? LastLoginAt);

public sealed record AdminReportDto(Guid Id, string RoomCode, string RoomTitle, string Reason,
    ReportStatus Status, DateTime CreatedAt);

public sealed record AdminErrorDto(long Id, DateTime At, string Level, string Category, string Message, string? Exception);
