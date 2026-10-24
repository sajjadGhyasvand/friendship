using System.Security.Cryptography;
using System.Text;
using FriendShip.Domain.Discovery;
using FriendShip.Domain.Rooms;

namespace FriendShip.Application.Discovery;

/// <summary>Pure, repeatable rules. Evidence strength is coverage, not a psychological probability.</summary>
public sealed class DiscoveryEngine
{
    public const string Version = "discovery-rules-v1";
    public const int MinimumTraitAnswers = 3;
    private static readonly (string Key, string Title, string Left, string Right)[] Dimensions =
    [ ("EI", "انرژی اجتماعی", "خلوت و جمع کوچک", "معاشرت و جمع"),
      ("JP", "سبک برنامه‌ریزی", "انعطاف و تصمیم در مسیر", "برنامه مشخص"),
      ("SN", "توجه در تجربه‌ها", "ایده‌ها و امکان‌ها", "جزئیات و تجربه عینی"),
      ("TF", "سبک سنجیدن انتخاب‌ها", "ارزش‌ها و اثر بر آدم‌ها", "معیارها و تحلیل پیامد"),
      ("novelty", "تجربه‌های تازه", "انتخاب‌های آشنا", "کشف و تجربه تازه") ];

    public static string ResultId(string key, IEnumerable<AnswerEvidence> evidence) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Version + ":" + key + ":" +
            string.Join("|", evidence.OrderBy(e => e.Key).Select(e => $"{e.Key}:{e.QuestionVersion}:{e.Revision}:{e.Value}")))))[..40];

    public IReadOnlyList<AnswerEvidence> ReadEvidence(IEnumerable<DiscoveryAnswer> own, Room? game, DiscoveryProfile settings)
    {
        var result = new Dictionary<string, AnswerEvidence>(StringComparer.Ordinal);
        if (game is not null)
        {
            foreach (var question in game.Questions)
            {
                var definition = question.TemplateKey is not null ? DiscoveryCatalog.Find(question.TemplateKey)
                    : DiscoveryCatalog.All.FirstOrDefault(q => q.Prompt == question.OwnerPrompt);
                if (definition is null || definition.Sensitive || definition.FreeText ||
                    (definition.MbtiOnly && !settings.AllowMbti)) continue;
                if (question.TemplateVersion is not null && question.TemplateVersion != DiscoveryQuestion.Version) continue;
                var option = question.Options.FirstOrDefault(o => o.Id == question.OwnerAnswerOptionId);
                var index = option is null ? -1 : Array.IndexOf(definition.Options, option.Text);
                if (index < 0 || question.OwnerPrompt != definition.Prompt) continue;
                result[definition.Key] = new(definition.Key, definition.Prompt, option!.Text, index,
                    DiscoveryQuestion.Version, "game-owner", question.Id.ToString("N"), game.CreatedAt, false);
            }
        }
        foreach (var answer in own)
        {
            // An explicit skip overrides the same older game preference; it is never imputed.
            result.Remove(answer.QuestionKey);
            var question = DiscoveryCatalog.Find(answer.QuestionKey);
            if (question is null || answer.QuestionVersion != DiscoveryQuestion.Version || answer.IsSkipped ||
                string.IsNullOrWhiteSpace(answer.Value) || question.Prompt != answer.Prompt ||
                (question.Sensitive && !settings.AllowRelationships) || (question.MbtiOnly && !settings.AllowMbti)) continue;
            if (!question.FreeText && (answer.OptionIndex is not int index || index < 0 || index >= question.Options.Length ||
                question.Options[index] != answer.Value)) continue;
            result[question.Key] = new(question.Key, question.Prompt, answer.Value, answer.OptionIndex,
                answer.QuestionVersion, "self-discovery", answer.Revision.ToString("N"), answer.AnsweredAt, question.Sensitive);
        }
        return result.Values.OrderBy(e => e.Key).ToArray();
    }

    public SelfAnalysis Analyze(IReadOnlyList<AnswerEvidence> evidence, bool mbti,
        IReadOnlyList<AnalysisDecision>? decisions = null)
    {
        var known = evidence.Where(e => e.Source is "self-discovery" or "game-owner" &&
                e.QuestionVersion == DiscoveryQuestion.Version && DiscoveryCatalog.Find(e.Key) is { } q && q.Prompt == e.Prompt &&
                (q.FreeText ? !string.IsNullOrWhiteSpace(e.Value) && e.Value.Length <= 100 :
                    e.OptionIndex is int option && option >= 0 && option < q.Options.Length && q.Options[option] == e.Value))
            .GroupBy(e => e.Key).Select(g => g.OrderByDescending(e => e.At).First())
            .Select(e => e with { Sensitive = DiscoveryCatalog.Find(e.Key)!.Sensitive }).ToArray();
        var interests = known.Where(e => DiscoveryCatalog.Find(e.Key)!.InterestKey is not null).Select(e =>
        {
            var q = DiscoveryCatalog.Find(e.Key)!;
            return new InterestPreference(q.InterestKey!, q.Category, e.Value,
                q.Direct ? "اظهار مستقیم" : "انتخاب در موقعیت فرضی", e.Key, e.At);
        }).ToArray();
        var traits = Dimensions.Select(d => Estimate(d.Key, d.Title, d.Left, d.Right, known)).ToArray();
        var facts = PersonalityComposer.Facts(known, traits, decisions).ToList();
        // Private evidence stays available internally; it never enters a personality story.
        var sensitive = known.Where(e => e.Sensitive).ToArray();
        if (sensitive.Length > 0)
            facts.Add(new(ResultId("private-choices", sensitive), "انتخاب‌های خصوصی تو",
                "این بخش برای خودت می‌مونه؛ قرار نیست از انتخاب‌های خصوصی‌ات یک برچسب عمومی بسازیم.",
                "فقط برای تو", "روابط", sensitive.Length, null, true, false, sensitive));
        MbtiEstimate? estimate = null;
        if (mbti)
        {
            var axes = new[] { "EI", "SN", "TF", "JP" }.Select(k => traits.Single(t => t.Key == k)).ToArray();
            var sufficient = axes.All(a => a.Percent.HasValue && Math.Abs(a.Percent.Value - 50) >= 10);
            var type = sufficient ? string.Concat(axes.Select((a, i) => a.Percent >= 50 ? "ESTJ"[i] : "INFP"[i])) : null;
            var key = ResultId("mbti", axes.SelectMany(a => a.Evidence));
            var decision = decisions?.FirstOrDefault(d => d.ResultKey == key);
            estimate = new(key, type, axes, decision?.State, decision?.Correction);
        }
        var byId = (decisions ?? []).ToDictionary(d => d.ResultKey);
        var decided = facts.Select(f => byId.TryGetValue(f.Id, out var d) ? f with { Decision = d.State,
            ShareCode = d.State == AnalysisDecisionState.Rejected ? null : d.ShareCode } : f).ToArray();
        return new(Version, known.Length == 0 ? null : known.Max(e => e.At), interests,
            traits.Where(t => mbti || t.Key is not ("SN" or "TF")).ToArray(), estimate,
            decided, known, PersonalityComposer.Portrait(decided.Where(f => !f.Sensitive).ToArray(), traits));
    }

    private static TraitEstimate Estimate(string key, string title, string left, string right, IReadOnlyList<AnswerEvidence> evidence)
    {
        var matching = evidence.Where(e => DiscoveryCatalog.Find(e.Key) is { } q && q.Dimension == key &&
            e.OptionIndex is int index && q.Scores is not null && index >= 0 && index < q.Scores.Length).ToArray();
        var score = matching.Length < MinimumTraitAnswers ? (int?)null :
            (int)Math.Round(50 + matching.Average(e => DiscoveryCatalog.Find(e.Key)!.Scores![e.OptionIndex!.Value]) / 2, MidpointRounding.AwayFromZero);
        return new(key, title, left, right, score, matching.Length,
            matching.Length < MinimumTraitAnswers ? "داده کافی نیست" : matching.Length < 5 ? "پوشش اولیه؛ سه شاهد مستقل" : "پوشش بیشتر؛ همچنان غیررسمی", matching);
    }
}
