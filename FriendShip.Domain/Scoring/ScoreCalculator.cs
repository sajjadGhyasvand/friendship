using FriendShip.Domain.Rooms;

namespace FriendShip.Domain.Scoring;

public enum ScoreTier { Stranger = 0, Acquaintance = 1, Friend = 2, GoodFriend = 3, BestFriend = 4, SoulMate = 5 }

public static class ScoreCalculator
{
    public const int MaxPointsPerQuestion = 100;
    public const int MinCorrectSpeedPoints = 80;

    public static int Percent(int correct, int total) =>
        total <= 0 ? 0 : (int)Math.Round(correct * 100.0 / total, MidpointRounding.AwayFromZero);

    /// <summary>Classic: 100 per correct answer. Speed: 80–100 depending on how fast.</summary>
    public static int PointsFor(bool correct, GameMode mode, TimeSpan elapsed, int? timeLimitSeconds)
    {
        if (!correct) return 0;
        if (mode == GameMode.Classic || timeLimitSeconds is null or <= 0) return MaxPointsPerQuestion;

        var ratio = Math.Clamp(elapsed.TotalSeconds / timeLimitSeconds.Value, 0, 1);
        var bonus = (MaxPointsPerQuestion - MinCorrectSpeedPoints) * (1 - ratio);
        return MinCorrectSpeedPoints + (int)Math.Round(bonus, MidpointRounding.AwayFromZero);
    }

    public static ScoreTier TierFor(int percent) => percent switch
    {
        >= 100 => ScoreTier.SoulMate,
        >= 81 => ScoreTier.BestFriend,
        >= 61 => ScoreTier.GoodFriend,
        >= 41 => ScoreTier.Friend,
        >= 21 => ScoreTier.Acquaintance,
        _ => ScoreTier.Stranger
    };
}
