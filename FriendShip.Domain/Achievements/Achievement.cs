using FriendShip.Domain.Common;

namespace FriendShip.Domain.Achievements;

public static class AchievementKeys
{
    public const string FirstQuiz = "first_quiz";
    public const string PerfectScore = "perfect_score";
    public const string FiveRooms = "five_rooms";
    public const string TenFriends = "ten_friends";
}

/// <summary>Stored only for registered users. Unique per (UserId, Key).</summary>
public class Achievement
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Key { get; private set; } = default!;
    public DateTime UnlockedAt { get; private set; }

    private Achievement() { }

    public static Achievement Unlock(Guid userId, string key, DateTime now)
    {
        Guard.Against(userId == Guid.Empty, "achievement.user", "کاربر نامعتبر است.");
        Guard.Against(string.IsNullOrWhiteSpace(key) || key.Length > 50, "achievement.key", "نشان نامعتبر است.");
        return new Achievement { Id = Guid.NewGuid(), UserId = userId, Key = key, UnlockedAt = now };
    }
}
