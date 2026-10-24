using FriendShip.Domain.Discovery;

namespace FriendShip.Application.Discovery;

public sealed record AnswerEvidence(string Key, string Prompt, string Value, int? OptionIndex,
    string QuestionVersion, string Source, string Revision, DateTime At, bool Sensitive);
public sealed record InterestPreference(string Key, string Category, string Value, string Source,
    string EvidenceKey, DateTime UpdatedAt);
public sealed record TraitEstimate(string Key, string Title, string LeftLabel, string RightLabel,
    int? Percent, int EvidenceCount, string EvidenceStrength, IReadOnlyList<AnswerEvidence> Evidence);
public sealed record AnalysisFact(string Id, string Title, string Text, string Kind, string Category,
    int SampleCount, int? Score, bool Sensitive, bool CanShare, IReadOnlyList<AnswerEvidence> Evidence,
    AnalysisDecisionState? Decision = null, string? ShareCode = null,
    FactCategory CategoryKey = FactCategory.Other, string VisualKey = "spark", string Emoji = "✨",
    FactCertainty Certainty = FactCertainty.FirstClue, string RuleKey = "", string StoryText = "",
    bool IsCombination = false, string PositiveText = "");
public sealed record MbtiEstimate(string ResultKey, string? Type, IReadOnlyList<TraitEstimate> Axes,
    AnalysisDecisionState? Decision = null, string? UserCorrection = null);
public sealed record SelfAnalysis(string AlgorithmVersion, DateTime? UpdatedAt,
    IReadOnlyList<InterestPreference> Interests, IReadOnlyList<TraitEstimate> Traits,
    MbtiEstimate? Mbti, IReadOnlyList<AnalysisFact> Facts, IReadOnlyList<AnswerEvidence> Evidence,
    PersonalityPortrait? Portrait = null);
public sealed record DiscoveryView(DiscoveryProfile? Settings, SelfAnalysis? Analysis,
    IReadOnlyList<DiscoveryAnswer> Answers, string OwnerName, int RemainingCount);
public sealed record CategoryKnowledge(string Category, int Correct, int Comparable, int Skipped,
    int? Percent, IReadOnlyList<int> QuestionOrders);
public sealed record KnowledgeAnalysis(string Version, int Correct, int Comparable, int Excluded,
    int? Percent, IReadOnlyList<CategoryKnowledge> Categories, string? BestCategory, string? LeastKnownCategory);
public sealed record FriendFact(string Id, string Title, string Text, string Category, int SampleCount,
    int? Score, IReadOnlyList<int> QuestionOrders);
public sealed record ComparisonItem(string Key, string Category, string YourValue, string TheirValue, bool Same);
public sealed record ProfileComparison(string OwnerName, int ComparableCount, int SameCount,
    IReadOnlyList<ComparisonItem> Preferences, IReadOnlyList<FriendFact> Facts);
public sealed record SharedFact(string OwnerName, AnalysisFact Fact, string Version);

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum FactCategory { Other, Music, Gaming, Film, Books, Animals, Travel, Food, Sport, Style, Social, Planning, Adventure, Lifestyle, Creativity }
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum FactCertainty { FirstClue, StatedPreference, SupportedPattern }
public sealed record PersonalityPortrait(string Nickname, string Description, string Emoji, string VisualKey,
    string Coverage, string PositiveLine, IReadOnlyList<string> Unknowns,
    IReadOnlyList<string>? Strengths = null, IReadOnlyList<string>? SignatureInterests = null, string HumorLine = "");
/// <summary>Render-safe output: never contains raw evidence, participant identities, options or account IDs.</summary>
public sealed record StoryFact(string Title, string Text, FactCategory CategoryKey, string VisualKey, string Emoji,
    string RuleKey = "", int? SampleCount = null)
{
    public static StoryFact From(AnalysisFact fact) => new(fact.Title, fact.StoryText, fact.CategoryKey, fact.VisualKey, fact.Emoji, fact.RuleKey);
}
public sealed record StoryContent(string Kind, string OwnerName, string Title, string Description, string Emoji,
    string VisualKey, string PositiveLine, string Invitation, IReadOnlyList<StoryFact> Facts, int? Percent = null, int? SampleCount = null);
public sealed record FeedbackQuality(string RuleKey, string CopyVersion, int Voters, int Confirmed, int Partial, int Rejected, int? ConfirmationPercent);
/// <summary>Internal consented, directly declared preferences. Never serialize this as a story.</summary>
public sealed record ConsentedPreferences(Guid UserId, IReadOnlyList<AnswerEvidence> Answers);
