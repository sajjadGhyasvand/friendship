using FriendShip.Domain.Participants;
using FriendShip.Domain.Rooms;
using FriendShip.Application.Questions;

namespace FriendShip.Application.Discovery;

public sealed class KnowledgeEngine
{
    public const string Version = "knowledge-comparable-v2";
    public const int MinimumStoryParticipants = 5;

    /// <summary>Public story projection. Never copies names, question text, options or private owner facts.</summary>
    public StoryContent? Story(Room game, IReadOnlyList<Participant> participants, string owner)
    {
        var completed = participants.Where(p => p.RoomId == game.Id && p.IsCompleted).DistinctBy(p => p.Id).ToArray();
        var safe = game.Questions.Select(q => (Question: q, Definition: DiscoveryCatalog.Find(q.TemplateKey ?? "")))
            .Where(x => x.Definition is { Sensitive: false, MbtiOnly: false, FreeText: false } &&
                x.Question.TemplateVersion == DiscoveryQuestion.Version && x.Question.OwnerPrompt == x.Definition.Prompt &&
                QuestionFactory.Catalog.Any(t => t.Key == x.Definition.Key && t.FriendPrompts.Contains(x.Question.Text)) &&
                x.Question.OwnerAnswerOptionId.HasValue && x.Question.HasOption(x.Question.OwnerAnswerOptionId.Value) &&
                x.Question.Options.Select(o => o.Text).ToHashSet().SetEquals(x.Definition.Options)).ToArray();
        var distributions = safe.Select(x =>
        {
            var answers = completed.Select(p => (p.Id, Answer: p.Answers.FirstOrDefault(a => a.RoomQuestionId == x.Question.Id &&
                a.IsFinal && !a.TimedOut && a.SelectedOptionId is Guid option && x.Question.HasOption(option))))
                .Where(a => a.Answer is not null).ToArray();
            return (Definition: x.Definition!, x.Question, Answers: answers, Correct: answers.Count(a => x.Question.IsCorrect(a.Answer!.SelectedOptionId)));
        }).Where(x => x.Answers.Length >= MinimumStoryParticipants).ToArray();
        if (distributions.Length < 2) return null;
        var sample = distributions.SelectMany(d => d.Answers.Select(a => a.Id)).Distinct().Count();
        var total = distributions.Sum(d => d.Answers.Length);
        var percent = Percent(distributions.Sum(d => d.Correct), total)!.Value;
        var facts = new List<StoryFact>();
        var surprises = distributions.Select(d => (Data: d, Popular: d.Answers.GroupBy(a => a.Answer!.SelectedOptionId)
            .OrderByDescending(g => g.Count()).First())).Where(x => x.Popular.Key != x.Data.Question.OwnerAnswerOptionId &&
                x.Popular.Count() * 2 > x.Data.Answers.Length)
            .OrderByDescending(x => x.Popular.Count() * 1.0 / x.Data.Answers.Length).ThenBy(x => x.Data.Question.Order).ToArray();
        foreach (var surprise in surprises)
        {
            var q = surprise.Data.Question;
            var ownerChoice = Array.IndexOf(surprise.Data.Definition.Options, q.Options.Single(o => o.Id == q.OwnerAnswerOptionId).Text);
            var popularChoice = Array.IndexOf(surprise.Data.Definition.Options, q.Options.Single(o => o.Id == surprise.Popular.Key).Text);
            var fact = Misconception(surprise.Data.Definition.Key, ownerChoice, popularChoice, surprise.Data.Answers.Length);
            if (fact is null) continue;
            facts.Add(fact); break;
        }
        foreach (var category in distributions.GroupBy(d => d.Definition.Category).Where(g => g.Count() >= 2)
                     .OrderByDescending(g => g.Sum(d => d.Answers.Length)).ThenBy(g => g.Key).Take(facts.Count == 0 ? 2 : 1))
        {
            var n = category.Sum(d => d.Answers.Length); var correct = category.Sum(d => d.Correct);
            var rate = Percent(correct, n)!.Value;
            var (key, visual, emoji) = category.Key switch
            {
                "موسیقی" => (FactCategory.Music, "music.headphones", "🎧"),
                "سرگرمی" => (FactCategory.Film, "film.screen", "🎬"),
                "استایل" => (FactCategory.Style, "style.shirt", "👕"),
                "سفر" => (FactCategory.Travel, "travel.suitcase", "✈️"),
                "خوراکی" => (FactCategory.Food, "food.pizza", "🍕"),
                _ => (FactCategory.Lifestyle, "spark", "✨")
            };
            facts.Add(new(category.Key, rate >= 70 ? $"توی {category.Key}، حدس‌ها روی فرم بودن: {rate}٪ درست!" :
                rate <= 35 ? $"{category.Key} هنوز جا برای غافلگیری داره؛ {rate}٪ حدس درست!" :
                $"{category.Key}؛ بخشی آشنا، بخشی غافلگیرکننده: {rate}٪ حدس درست!", key, visual, emoji));
        }
        var familiar = distributions.Count(d => d.Correct * 100 >= d.Answers.Length * 70);
        if (facts.Count < 2)
            facts.Add(new("سرنخ‌های آشنا", familiar > 0 ? $"در {familiar} سؤال، حدس‌های درست به ۷۰٪ یا بیشتر رسیدن؛ این تکه‌ها رو خوب می‌شناسن!" :
                "هنوز هیچ سؤالِ این جمع‌بندی به ۷۰٪ حدس درست نرسیده؛ بهونه برای بیشتر شناختن کم نیست!", FactCategory.Other, "social.spark", "💡"));
        facts.Add(new("قصه ادامه داره", percent >= 70 ? "سرنخ‌ها خوب گرفته شدن؛ پرونده برای یک دور تازه جا داره!" :
            percent <= 35 ? "این دور، قسمت اولِ یک معما بود؛ قسمت بعدی با یک گپ دوستانه!" :
            "بخشی از رمز رو گرفتن، بقیه‌اش به گپ بعدی حواله شد!", FactCategory.Other, "social.detective", "🕵️", "friends.playful", sample));
        return new("friends", owner, "رفیقام، کارآگاه‌های زندگی من!", $"از {sample} بازی تکمیل‌شده؛ {percent}٪ از {total} حدس معتبر درست بوده.",
            "🕵️", "social.detective", "رفاقت بیشتر از یک امتیازه؛ این فقط بازیِ حدس‌هاست 💚",
            "فکر می‌کنی بهتر حدس می‌زنی؟ بیا امتحان کن!", facts, percent, sample);
    }

    private static StoryFact? Misconception(string key, int owner, int popular, int sample)
    {
        (string Title, string Text, FactCategory Category, string Visual, string Emoji)? copy = key switch
        {
            "weekend" when owner == 0 && popular == 1 => ("آخرهفتهٔ غافلگیرکننده", "حدس غالب سمت دورهمی رفت؛ خودم برای آخرهفته یک برنامهٔ آرام‌تر انتخاب کردم!", FactCategory.Lifestyle, "social.moon", "🌙"),
            "music-mood" when owner == 0 && popular == 1 => ("پلی‌لیستِ خلاف انتظار", "بیشتر حدس‌ها این پلی‌لیست رو آرام‌تر تصور کردن؛ انتخاب من پرانرژی‌تر بود!", FactCategory.Music, "music.headphones", "🎧"),
            "music" => ("ملودیِ غافلگیرکننده", "بیشتر حدس‌ها برای سبک موسیقی‌ام یک مسیر دیگه رفتن؛ هدفونم هنوز جای کشف داره!", FactCategory.Music, "music.headphones", "🎧"),
            "music-context" => ("هدفون، کِی؟", "بیشتر حدس‌ها دربارهٔ وقتِ موسیقی گوش‌دادنم یک سناریوی دیگه داشتن؛ موسیقی متنم غافلگیرشون کرد!", FactCategory.Music, "music.headphones", "🎧"),
            "free-time" when owner == 0 && popular == 1 => ("ریموت یا دستهٔ بازی؟", "حدس غالب وقت آزادم رو سینمایی دید؛ انتخاب خودم یک مرحلهٔ بازی بود!", FactCategory.Gaming, "gaming.controller", "🎮"),
            "planning" when owner == 1 && popular == 0 => ("تقویمِ قابل ویرایش", "بیشتر حدس‌ها برنامهٔ ازقبل‌چیده می‌خواستن؛ انتخاب خودم تصمیم در لحظه بود!", FactCategory.Planning, "planning.calendar", "🗓️"),
            "movie" => ("ژانرِ پیچ داستان", "ژانر فیلمی که انتخاب کردم با حدسِ بیشتر رفقا یکی نبود؛ پیچ داستان همین‌جاست!", FactCategory.Film, "film.screen", "🎬"),
            "travel" => ("چمدونِ خلاف انتظار", "مقصدِ انتخابی من با حدس بیشتر رفقا فرق داشت؛ روی نقشه هنوز غافلگیری داریم!", FactCategory.Travel, "travel.suitcase", "✈️"),
            "style-priority" => ("استایل، از نگاه خودم", "بیشتر حدس‌ها معیارِ انتخاب لباسم رو متفاوت تصور کردن؛ استایل، امضای خودمه!", FactCategory.Style, "style.shirt", "👕"),
            _ => ((string, string, FactCategory, string, string)?)null
        };
        return copy is { } c ? new(c.Title, c.Text, c.Category, c.Visual, c.Emoji, "perception." + key, sample) : null;
    }

    public IReadOnlyList<StoryFact> SharedInterests(ConsentedPreferences owner, IReadOnlyList<ConsentedPreferences> friends)
    {
        static Dictionary<string, AnswerEvidence> Direct(ConsentedPreferences profile) => profile.Answers.Where(e =>
            e.Source == "self-discovery" && !e.Sensitive && e.QuestionVersion == DiscoveryQuestion.Version &&
            DiscoveryCatalog.Find(e.Key) is { Direct: true, Sensitive: false, MbtiOnly: false, FreeText: false, InterestKey: not null } q &&
            q.Prompt == e.Prompt && e.OptionIndex is int n && n >= 0 && n < q.Options.Length && q.Options[n] == e.Value)
            .GroupBy(e => e.Key).ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.At).First());
        var mine = Direct(owner);
        var peers = friends.Where(f => f.UserId != owner.UserId).DistinctBy(f => f.UserId).Select(Direct).ToArray();
        if (peers.Length < MinimumStoryParticipants) return [];
        return mine.Values.GroupBy(e => DiscoveryCatalog.Find(e.Key)!.Category).Where(g => g.Count() >= 2)
            .Select(g => (Category: g.Key, Count: peers.Count(p => g.Count(e => p.TryGetValue(e.Key, out var other) && other.Value == e.Value) >= 2)))
            .Where(g => g.Count >= MinimumStoryParticipants).OrderByDescending(g => g.Count).ThenBy(g => g.Category)
            .Select(g => g.Category switch
            {
                "موسیقی" => new StoryFact("یک پلی‌لیست مشترک؟", $"با {g.Count} رفیق، دست‌کم دو انتخاب موسیقی یکسان داریم؛ پلی‌لیستِ گپ بعدی آماده‌ست!", FactCategory.Music, "music.headphones", "🎧", "shared.music", g.Count),
                "استایل" => new StoryFact("هم‌سلیقه‌های خوش‌پوش", $"با {g.Count} رفیق، دست‌کم دو انتخاب استایل یکسان داریم؛ قرارِ هم‌رنگ خیلی هم دور نیست!", FactCategory.Style, "style.shirt", "👕", "shared.style", g.Count),
                "خوراکی" => new StoryFact("قرارِ خوشمزه", $"با {g.Count} رفیق، دست‌کم دو انتخاب خوردنی و نوشیدنی یکسان داریم؛ هماهنگیِ منو یک قدم جلوئه!", FactCategory.Food, "food.pizza", "🍕", "shared.food", g.Count),
                "سرگرمی" => new StoryFact("تفریح، با سلیقهٔ مشترک", $"با {g.Count} رفیق، دست‌کم دو انتخاب سرگرمی یکسان داریم؛ برنامهٔ جمعه از صفر شروع نمی‌شه!", FactCategory.Lifestyle, "social.spark", "✨", "shared.leisure", g.Count),
                "عادت‌ها" => new StoryFact("هم‌ریتم‌های روزمره", $"با {g.Count} رفیق، هم زمانِ انرژی و هم فضای استراحتمون جور درمی‌آد؛ هم‌ریتم پیدا شد!", FactCategory.Lifestyle, "social.moon", "🌙", "shared.habits", g.Count),
                _ => null
            }).OfType<StoryFact>().Take(1).ToArray();
    }
    public KnowledgeAnalysis Analyze(Room game, IEnumerable<ParticipantAnswer> answers)
    {
        var questions = game.Questions.ToDictionary(q => q.Id);
        var final = answers.Where(a => a.IsFinal && questions.ContainsKey(a.RoomQuestionId))
            .GroupBy(a => a.RoomQuestionId).Select(g => g.First()).ToArray();
        var comparable = final.Where(a => a.SelectedOptionId is Guid option && !a.TimedOut &&
            questions[a.RoomQuestionId].OwnerAnswerOptionId.HasValue && questions[a.RoomQuestionId].HasOption(option)).ToArray();
        var categories = game.Questions.GroupBy(q => q.CategoryName).Select(group =>
        {
            var ids = group.Select(q => q.Id).ToHashSet();
            var included = comparable.Where(a => ids.Contains(a.RoomQuestionId)).ToArray();
            var correct = included.Count(a => questions[a.RoomQuestionId].IsCorrect(a.SelectedOptionId));
            return new CategoryKnowledge(group.Key, correct, included.Length, group.Count() - included.Length,
                included.Length >= 2 ? Percent(correct, included.Length) : null,
                included.Select(a => questions[a.RoomQuestionId].Order).Order().ToArray());
        }).ToArray();
        var totalCorrect = comparable.Count(a => questions[a.RoomQuestionId].IsCorrect(a.SelectedOptionId));
        var eligible = categories.Where(c => c.Percent.HasValue).ToArray();
        var best = eligible.OrderByDescending(c => c.Percent).ThenBy(c => c.Category).FirstOrDefault();
        var least = eligible.OrderBy(c => c.Percent).ThenBy(c => c.Category).FirstOrDefault();
        return new(Version, totalCorrect, comparable.Length, game.Questions.Count - comparable.Length,
            Percent(totalCorrect, comparable.Length), categories, best?.Category,
            eligible.Length > 1 && least?.Percent < best?.Percent ? least.Category : null);
    }

    public IReadOnlyList<FriendFact> OwnerFacts(Room game, IReadOnlyList<Participant> completed)
    {
        var analyses = completed.Where(p => p.IsCompleted).Select(p => (Participant: p, Knowledge: Analyze(game, p.Answers))).ToArray();
        var facts = new List<FriendFact>();
        // Fair winners have answered the same full question set; a single correct answer cannot win over ten.
        var fullyComparable = analyses.Where(a => a.Knowledge.Comparable == game.Questions.Count).ToArray();
        if (fullyComparable.Length >= 2)
        {
            var maximum = fullyComparable.Max(a => a.Knowledge.Correct);
            var winners = fullyComparable.Where(a => a.Knowledge.Correct == maximum).OrderBy(a => a.Participant.Id).ToArray();
            if (maximum > 0)
                facts.Add(new("overall-leaders", "دقیق‌ترین حدس‌ها در این دور",
                    $"{Names(winners.Select(w => w.Participant.DisplayName))} با {maximum} پاسخ درست از {game.Questions.Count} سؤال، بیشترین حدس درست را در {fullyComparable.Length} بازی قابل مقایسه داشته‌اند.",
                    "کل بازی", fullyComparable.Length, Percent(maximum, game.Questions.Count), game.Questions.Select(q => q.Order).ToArray()));
        }
        foreach (var group in game.Questions.GroupBy(q => q.CategoryName).Where(g => g.Count() >= 2))
        {
            var peers = analyses.Select(a => (a.Participant, Category: a.Knowledge.Categories.Single(c => c.Category == group.Key)))
                .Where(a => a.Category.Comparable == group.Count()).ToArray();
            if (peers.Length < 2) continue;
            var maximum = peers.Max(a => a.Category.Correct);
            if (maximum == 0) continue;
            var winners = peers.Where(a => a.Category.Correct == maximum).OrderBy(a => a.Participant.Id).ToArray();
            facts.Add(new("category-leaders:" + group.Key, "بیشترین شناخت در " + group.Key,
                $"{Names(winners.Select(w => w.Participant.DisplayName))} در این دسته {maximum} پاسخ درست از {group.Count()} سؤال داشته‌اند؛ مقایسه بین {peers.Length} بازی با سؤال‌های یکسان است.",
                group.Key, peers.Length, Percent(maximum, group.Count()), group.Select(q => q.Order).ToArray()));
        }
        var distributions = game.Questions.Select(q =>
        {
            var valid = completed.Where(p => p.IsCompleted).SelectMany(p => p.Answers).Where(a => a.RoomQuestionId == q.Id &&
                a.IsFinal && !a.TimedOut && a.SelectedOptionId is Guid option && q.HasOption(option)).ToArray();
            return (Question: q, Answers: valid, Correct: valid.Count(a => q.IsCorrect(a.SelectedOptionId)));
        }).Where(d => d.Answers.Length >= 5).ToArray();
        if (distributions.Length > 0)
        {
            var hardest = distributions.OrderBy(d => (double)d.Correct / d.Answers.Length).ThenBy(d => d.Question.Order).First();
            facts.Add(new("unexpected-question", "یک انتخاب غافلگیرکننده",
                $"در سؤال «{hardest.Question.Text}»، {hardest.Correct} نفر از {hardest.Answers.Length} پاسخ معتبر، انتخابت را درست حدس زده‌اند.",
                hardest.Question.CategoryName, hardest.Answers.Length, Percent(hardest.Correct, hardest.Answers.Length), [hardest.Question.Order]));
            var common = hardest.Answers.GroupBy(a => a.SelectedOptionId!.Value).OrderByDescending(g => g.Count()).ToArray();
            if (common.Length > 0 && (common.Length == 1 || common[0].Count() > common[1].Count()))
            {
                var option = hardest.Question.Options.Single(o => o.Id == common[0].Key);
                facts.Add(new("popular-guess", "محبوب‌ترین حدس، نه سلیقه دوست‌ها",
                    $"در همین سؤال، «{option.Text}» پرتکرارترین حدس بوده: {common[0].Count()} از {hardest.Answers.Length} پاسخ. این درباره حدس آن‌هاست، نه علاقه خودشان.",
                    hardest.Question.CategoryName, hardest.Answers.Length, Percent(common[0].Count(), hardest.Answers.Length), [hardest.Question.Order]));
            }
        }
        return facts.GroupBy(f => f.Id).Select(g => g.First()).ToArray();
    }
    private static string Names(IEnumerable<string> names)
    {
        var list = names.ToArray();
        return string.Join("، ", list.Take(3)) + (list.Length > 3 ? $" و {list.Length - 3} نفر دیگر" : "");
    }
    private static int? Percent(int correct, int total) => total <= 0 ? null :
        (int)Math.Round(correct * 100.0 / total, MidpointRounding.AwayFromZero);
}
