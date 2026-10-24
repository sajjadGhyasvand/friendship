using System.Security.Cryptography;
using System.Text;

namespace FriendShip.Application.Discovery;

public sealed class AdaptiveQuestionSelector
{
    public IReadOnlyList<DiscoveryQuestion> Available(IEnumerable<AnswerEvidence> evidence, IEnumerable<string> seen,
        bool relationships, bool mbti)
    {
        var answers = evidence.ToDictionary(e => e.Key);
        var excluded = seen.Concat(answers.Keys).ToHashSet(StringComparer.Ordinal);
        var declinedRelationships = excluded.Any(k => DiscoveryCatalog.Find(k)?.Sensitive == true && !answers.ContainsKey(k));
        return DiscoveryCatalog.All.Where(q => !excluded.Contains(q.Key) && (!q.Sensitive || relationships && !declinedRelationships) &&
            (!q.MbtiOnly || mbti) && (q.FollowKey is null || answers.TryGetValue(q.FollowKey, out var parent) &&
                parent.OptionIndex is int choice && q.FollowChoices!.Contains(choice))).ToArray();
    }

    public DiscoveryQuestion? Next(Guid userId, IReadOnlyList<AnswerEvidence> evidence, IEnumerable<string> seen,
        bool relationships, bool mbti, string? category = null)
    {
        var counts = evidence.GroupBy(e => DiscoveryCatalog.Find(e.Key)?.Category ?? "").ToDictionary(g => g.Key, g => g.Count());
        var dimensions = evidence.Where(e => DiscoveryCatalog.Find(e.Key)?.Dimension is not null)
            .GroupBy(e => DiscoveryCatalog.Find(e.Key)!.Dimension!).ToDictionary(g => g.Key, g => g.Count());
        return Available(evidence, seen, relationships, mbti).Where(q => category is null || q.Category == category)
            .OrderByDescending(q =>
                (q.FollowKey is not null ? 5 : 0) +
                (q.Dimension is not null && dimensions.GetValueOrDefault(q.Dimension) < 3 ? 5 : 0) -
                counts.GetValueOrDefault(q.Category) * 2)
            .ThenBy(q => Tie(userId, q.Key)).FirstOrDefault();
    }

    public IReadOnlyList<string> SelectGameKeys(Guid userId, IReadOnlyList<AnswerEvidence> evidence,
        IEnumerable<string> previousKeys, int count = 10)
    {
        var previous = previousKeys.ToHashSet();
        var result = new List<DiscoveryQuestion>();
        var candidates = DiscoveryCatalog.All.Where(q =>
            FriendShip.Application.Questions.QuestionFactory.Catalog.Any(t => t.Key == q.Key)).ToList();
        var interestCategories = evidence.Select(e => DiscoveryCatalog.Find(e.Key)?.Category).ToHashSet();
        // Reserve pairs in several themes, so a ten-question game can support category analysis.
        var themes = candidates.GroupBy(q => q.Category).Where(g => g.Count() >= 2)
            .OrderBy(g => g.Count(q => previous.Contains(q.Key)) * 3 - (interestCategories.Contains(g.Key) ? 2 : 0))
            .ThenBy(g => Tie(userId, "category:" + g.Key)).Take(Math.Min(4, Math.Max(0, count / 2))).ToArray();
        foreach (var theme in themes)
        {
            var pair = theme.OrderBy(q => previous.Contains(q.Key)).ThenBy(q => Tie(userId, q.Key)).Take(2).ToArray();
            result.AddRange(pair);
            foreach (var question in pair) candidates.Remove(question);
        }
        while (result.Count < count && candidates.Count > 0)
        {
            var next = candidates.OrderBy(q => result.Count(x => x.Category == q.Category) * 10 +
                    (previous.Contains(q.Key) ? 3 : 0) - (interestCategories.Contains(q.Category) ? 2 : 0))
                .ThenBy(q => Tie(userId, q.Key)).First();
            result.Add(next); candidates.Remove(next);
        }
        return result.Select(q => q.Key).ToArray();
    }
    private static string Tie(Guid user, string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user + ":" + key)));
}
