using FriendShip.Domain.Common;

namespace FriendShip.Domain.Discovery;

public sealed class DiscoveryProfile
{
    public Guid UserId { get; private set; }
    public bool Enabled { get; private set; }
    public bool AllowComparison { get; private set; }
    public bool AllowRelationships { get; private set; }
    public bool AllowMbti { get; private set; }
    public bool AllowAggregateSharing { get; private set; }
    public string? ComparisonCode { get; private set; }
    public Guid Version { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    private DiscoveryProfile() { }
    public static DiscoveryProfile Create(Guid userId) => new() { UserId = userId, Version = Guid.NewGuid() };
    public void Configure(bool enabled, bool compare, bool relationships, bool mbti, DateTime now, bool aggregateSharing = false)
    {
        Enabled = enabled;
        AllowRelationships = enabled && relationships;
        AllowMbti = enabled && mbti;
        AllowAggregateSharing = enabled && aggregateSharing;
        var wasAllowed = AllowComparison;
        AllowComparison = enabled && compare;
        ComparisonCode = AllowComparison ? (wasAllowed ? ComparisonCode : ShortCode.Generate(24)) : null;
        Touch(now);
    }
    public void Touch(DateTime now) { UpdatedAt = now; Version = Guid.NewGuid(); }
}

public sealed class DiscoveryAnswer
{
    public Guid UserId { get; private set; }
    public string QuestionKey { get; private set; } = "";
    public string QuestionVersion { get; private set; } = "";
    public string Prompt { get; private set; } = "";
    public int? OptionIndex { get; private set; }
    public string? Value { get; private set; }
    public bool IsSkipped { get; private set; }
    public bool IsSensitive { get; private set; }
    public DateTime AnsweredAt { get; private set; }
    public Guid Revision { get; private set; }
    private DiscoveryAnswer() { }
    public static DiscoveryAnswer Create(Guid user, string key) => new() { UserId = user, QuestionKey = key };
    public void Record(string version, string prompt, int? option, string? value, bool skipped, bool sensitive, DateTime now)
    {
        QuestionVersion = Guard.Text(version, 1, 32, "discovery.version", "نسخه سؤال معتبر نیست.");
        Prompt = Guard.Text(prompt, 5, 200, "discovery.prompt", "سؤال معتبر نیست.");
        OptionIndex = skipped ? null : option;
        Value = skipped ? null : Guard.Text(value, 1, 100, "discovery.value", "پاسخ معتبر نیست.");
        IsSkipped = skipped; IsSensitive = sensitive; AnsweredAt = now; Revision = Guid.NewGuid();
    }
}

public enum AnalysisDecisionState { Confirmed = 1, Rejected = 2, Corrected = 3, Partial = 4 }

public sealed class AnalysisDecision
{
    public Guid UserId { get; private set; }
    public string ResultKey { get; private set; } = "";
    public AnalysisDecisionState? State { get; private set; }
    public string? Correction { get; private set; }
    public string? ShareCode { get; private set; }
    public string? RuleKey { get; private set; }
    public string? CopyVersion { get; private set; }
    public int? EvidenceCount { get; private set; }
    public DateTime? FeedbackAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Guid Revision { get; private set; }
    private AnalysisDecision() { }
    public static AnalysisDecision Create(Guid user, string key) => new() { UserId = user, ResultKey = key };
    public void Set(AnalysisDecisionState state, string? correction, DateTime now)
    {
        State = state; Correction = correction; ShareCode = null; FeedbackAt = now; Touch(now);
    }
    public void Share(bool allow, DateTime now) { ShareCode = allow ? ShareCode ?? ShortCode.Generate(24) : null; Touch(now); }
    public void DescribeFact(string ruleKey, string copyVersion, int evidenceCount)
    {
        RuleKey = Guard.Text(ruleKey, 1, 64, "feedback.rule", "قاعده معتبر نیست.");
        CopyVersion = Guard.Text(copyVersion, 1, 32, "feedback.version", "نسخه معتبر نیست.");
        Guard.Against(evidenceCount < 1, "feedback.evidence", "شاهد لازم است.");
        EvidenceCount = evidenceCount;
    }
    private void Touch(DateTime now) { UpdatedAt = now; Revision = Guid.NewGuid(); }
}
