namespace FriendShip.Domain.Scoring;

public sealed record RankInput(Guid ParticipantId, int Score, int CorrectCount, TimeSpan Duration);
public sealed record RankEntry(Guid ParticipantId, int Rank);

public static class Ranking
{
    /// <summary>Competition ranking (1,1,3): higher score, then more correct, then faster. Equal on all = tie.</summary>
    public static IReadOnlyList<RankEntry> Compute(IEnumerable<RankInput> inputs)
    {
        var ordered = inputs
            .OrderByDescending(i => i.Score)
            .ThenByDescending(i => i.CorrectCount)
            .ThenBy(i => i.Duration)
            .ToList();

        var result = new List<RankEntry>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var rank = i + 1;
            if (i > 0)
            {
                var p = ordered[i - 1]; var c = ordered[i];
                if (p.Score == c.Score && p.CorrectCount == c.CorrectCount && p.Duration == c.Duration)
                    rank = result[i - 1].Rank;
            }
            result.Add(new RankEntry(ordered[i].ParticipantId, rank));
        }
        return result;
    }

    /// <summary>Percentile needs enough people to be meaningful; otherwise null.</summary>
    public static int? Percentile(int score, IReadOnlyCollection<int> allScores, int minSample = 5)
    {
        if (allScores.Count < minSample) return null;
        var below = allScores.Count(s => s < score);
        return (int)Math.Round(below * 100.0 / allScores.Count, MidpointRounding.AwayFromZero);
    }
}
