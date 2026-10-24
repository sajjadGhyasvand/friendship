using System.Security.Cryptography;
using System.Text;
using FriendShip.Domain.Discovery;

namespace FriendShip.Application.Discovery;

/// <summary>Bounded, evidence-backed copy rules. No random personality assignments or unasked preferences.</summary>
public static class PersonalityComposer
{
    public const string Version = "personality-copy-v1";
    public const int MaxFacts = 6;
    public const string PositiveLine = "سلیقه‌ات امضای خودت رو داره؛ همینش قشنگه ✨";

    public static IReadOnlyList<AnalysisFact> Facts(IReadOnlyList<AnswerEvidence> evidence, IReadOnlyList<TraitEstimate> traits,
        IReadOnlyList<AnalysisDecision>? decisions = null)
    {
        var known = evidence.Where(e => !e.Sensitive).ToDictionary(e => e.Key);
        var result = new List<AnalysisFact>();
        bool Is(string key, params int[] choices) => known.TryGetValue(key, out var e) && e.OptionIndex is int n && choices.Contains(n);
        void Add(string rule, FactCategory category, string visual, string emoji, string title, string shortText,
            string[] texts, params string[] keys)
        {
            var support = keys.Distinct().Where(known.ContainsKey).Select(k => known[k]).ToArray();
            if (support.Length == 0) return;
            var text = texts[Variant(rule, support, texts.Length)];
            result.Add(new(DiscoveryEngine.ResultId(Version + ":" + rule, support), title, text, "از انتخاب‌های خودت", Label(category),
                support.Length, null, false, true, support, CategoryKey: category, VisualKey: visual, Emoji: emoji,
                Certainty: FactCertainty.StatedPreference, RuleKey: rule, StoryText: shortText));
        }
        if (Is("music-mood", 0))
            Add("music.energy", FactCategory.Music, "music.headphones", "🎧", "سوختِ پلی‌لیستی", "موسیقی خوب، سوخت موشکمه!",
                ["پلی‌لیستت برای بقیه آهنگه؛ برای تو احتمالاً سوخت موشکه! 🚀", "دکمهٔ پخش رو که می‌زنی، انگار حالت پرواز روشن می‌شه!"], "music-mood", "music");
        else if (known.ContainsKey("music"))
        {
            var index = known["music"].OptionIndex!.Value;
            string[][] copies = [
                ["آهنگ خوب که بیاد، زندگی یه‌کم شبیه موزیک‌ویدئو می‌شه؛ فقط دوربین کم داریم!", "برای حال‌وهوای روزت، یک آهنگ خوش‌ریتم همیشه توی آستین داری!"],
                ["هدفونت بیشتر بلیت کنسرت راکه تا موسیقیِ پس‌زمینهٔ آسانسور!", "پلی‌لیستت با گیتار الکتریک سلام می‌کنه؛ ورود به کنسرتِ شخصی آزاده!"],
                ["پلی‌لیستت ریتم رو تحویل می‌گیره، با چند قافیه پس می‌ده!", "بعضی‌ها مسیر رو با تابلو می‌سنجن؛ پلی‌لیست تو با بیت و قافیه جلو می‌ره!"],
                ["وسط شلوغی روز، یک ارکستر کوچیک توی هدفونت منتظر اجراست!", "پلی‌لیستت کت‌وشلوار لازم نداره، ولی حال‌وهوای یک اجرای باشکوه رو چرا!" ] ];
            string[] shortCopies = ["زندگی با یک موسیقی متنِ پاپ", "هدفونم، بلیت کنسرت راکه", "ریتم و قافیه، چاشنیِ روزم", "یک ارکستر کوچک توی هدفونم"];
            Add("music.genre", FactCategory.Music, "music.headphones", "🎧", "دی‌جیِ دنیای خودت", shortCopies[index], copies[index], "music", "music-context");
        }
        else if (known.ContainsKey("music-context"))
            Add("music.companion", FactCategory.Music, "music.headphones", "🎧", "همراهِ هدفونی", "زندگی با یک موسیقی متن بهتره",
                ["برای بخشی از روزت موسیقی متن داری؛ تیتراژش رو هم خودت انتخاب می‌کنی!", "هدفونت فقط وسیله نیست؛ همراه یک تکه از روزته!"], "music-context");

        if (Is("free-time", 0) || known.ContainsKey("game-style"))
            Add("gaming.main", FactCategory.Gaming, "gaming.controller", "🎮", "یه دست دیگه!", "یه دست دیگه، اسم رمزِ تفریحه!",
                ["«یه دست دیگه» می‌تونه اسم فصل بعدیِ وقت آزادت باشه! 😏", "وقت آزاد که پیدا بشه، دستهٔ بازی زودتر از بقیه خبردار می‌شه!"], Is("free-time", 0) ? "free-time" : "", "game-style");
        if (Is("free-time", 1) || known.ContainsKey("movie") || known.ContainsKey("screen-world"))
            Add("film.main", FactCategory.Film, "film.screen", "🎬", "قرار با داستان‌ها", "یک داستان خوب، یک قرار خوبه",
                ["تفریح روی مبل هم می‌تونه ماجراجویی باشه؛ فقط ریموت رو بده به خودت!", "برای یک داستان خوب جا باز می‌کنی؛ پاپ‌کورن نقش مکمل داره!"], "movie", "screen-world", Is("free-time", 1) ? "free-time" : "");
        if (Is("free-time", 2) || known.ContainsKey("book-genre"))
            Add("books.main", FactCategory.Books, "books.stack", "📚", "سفر با ورق‌ها", "بلیتم برای سفر، گاهی یک کتابه",
                ["برای سفر به یک دنیای دیگه، گاهی فقط باز کردن یک کتاب کافیه!", "وقت آزاد و یک کتاب خوب؟ مأموریت فرعیِ دوست‌داشتنی پیدا شد!"], "book-genre", Is("free-time", 2) ? "free-time" : "");
        if (Is("animal", 0))
            Add("animals.cat", FactCategory.Animals, "animals.cat", "🐱", "تیمِ میو", "تیم گربه‌ها؛ با افتخار!",
                ["بین برنامه‌های یک روز آزاد، ملاقات با یک گربه شانس ویژه‌ای داره!", "برای یک قرار پنجه‌دار، تیم میو رو انتخاب می‌کنی؛ انتخاب قابل‌درکیه!"], "animal");
        else if (Is("animal", 1))
            Add("animals.dog", FactCategory.Animals, "animals.dog", "🐶", "تیمِ دم‌جنبون‌ها", "یک رفیق دم‌جنبون؟ بله لطفاً!",
                ["رفیقِ یک روز آزاد تو می‌تونه چهار تا پنجه و یک عالمه ذوق داشته باشه!", "تیم سگ‌ها یک رأی پرانرژی از طرف تو داره!"], "animal");
        if (Is("style-priority", 0) || Is("style-main", 0, 2))
            Add("style.comfort", FactCategory.Style, "style.shirt", "👕", "راحت و روی فرم", "استایل خوب باید راحت هم باشه",
                ["لباس راحت برای تو امتیاز ویژه داره؛ خفن بودن با زجر کشیدن قرارداد نداره!", "استایلی که توش راحت باشی، از همین اول یک قدم جلوئه!"], Is("style-priority", 0) ? "style-priority" : "", Is("style-main", 0, 2) ? "style-main" : "");
        else if (known.ContainsKey("style-main") || known.ContainsKey("style-colors"))
            Add("style.signature", FactCategory.Style, "style.shirt", "👕", "امضای پوشیدنی", "استایل، یک جور امضای شخصیه",
                ["ترکیب لباس‌ها هم می‌تونه امضای خودت رو داشته باشه؛ بدون نیاز به زیرنویس!", "توی انتخاب لباس، برای سلیقهٔ خودت هم صندلی رزرو کردی!"], "style-main", "style-colors");
        if (known.ContainsKey("travel"))
        {
            var nature = Is("travel", 0, 3);
            Add(nature ? "travel.nature" : "travel.escape", FactCategory.Travel, nature ? "travel.nature" : "travel.suitcase", nature ? "🌿" : "✈️",
                "چمدونِ حال خوب", nature ? "یک نفس تازه، بیرون از شلوغی" : "یک مقصد تازه، یک حال تازه",
                nature ? ["برای عوض کردن حال‌وهوا، طبیعت توی فهرستت جای خوبی داره؛ آنتن کمتر، منظره بیشتر!", "گاهی برنامهٔ خوب یعنی هوای آزاد، منظره بیشتر و کمی فاصله از شلوغی!" ]
                    : ["برای یک مقصد دوست‌داشتنی، چمدون ذهنیت آمادهٔ حرکت می‌شه!", "توی نقشهٔ حال خوبت، جا برای یک سفر هم هست!"], "travel");
        }
        if (known.ContainsKey("food"))
            Add("food.main", FactCategory.Food, "food.pizza", "🍕", "تفریحِ خوشمزه", "شام خوب، یک برنامهٔ حسابیه",
                ["یک غذای دوست‌داشتنی می‌تونه برنامهٔ شب رو نجات بده؛ تیتراژ پایانی با رضایت!", "توی فهرست خوش‌گذرونی‌هات، برای یک شام خوب هم جا هست!"], "food");
        if (Is("free-time", 3) || known.ContainsKey("sport-style"))
            Add("sport.main", FactCategory.Sport, "sport.ball", "🏀", "حال خوب در حرکت", "کمی حرکت، کمی حال بهتر",
                ["برای حال بهتر، گاهی دکمهٔ حرکت رو به دکمهٔ اسکرول ترجیح می‌دی!", "تفریح همیشه نشستن نیست؛ یک جای فهرستت برای حرکت رزرو شده!"], "sport-style", Is("free-time", 3) ? "free-time" : "");

        foreach (var trait in traits.Where(t => t.Percent.HasValue && (t.Key is "EI" or "JP" or "novelty")))
        {
            var quiet = trait.Percent <= 35; var outgoing = trait.Percent >= 65;
            if (trait.Key == "novelty" && !quiet && !outgoing) continue;
            var (rule, category, visual, emoji, title, shortText, copies) = trait.Key switch
            {
                "EI" when quiet => ("social.quiet", FactCategory.Social, "social.moon", "🌙", "شارژ با خلوت", "گاهی بهترین قرارم، خودم و وقت آزاده", new[] { "برای شارژ دوباره، گاهی خودت و چند ساعت وقت آزاد بهترین قرارید!", "وقت خلوتت هم برنامه‌ست؛ فقط مهمون اضافه دعوت نشده!" }),
                "EI" when outgoing => ("social.party", FactCategory.Social, "social.party", "🎉", "حال خوب با جمع", "یک جمع خوب، یک شارژ اساسی", new[] { "جمعِ خوب برای باتری اجتماعیت شارژرِ محبوبیه!", "گاهی حال خوبت یک دورهمی فاصله داره؛ صندلی اضافه یادت نره!" }),
                "EI" => ("social.balance", FactCategory.Social, "social.balance", "🌓", "کلید جمع و خلوت", "هم جمع خوبه، هم خلوتِ به‌موقع", new[] { "هم برای جمع جا داری، هم برای خلوت؛ بستگی داره باتری روی چند درصد باشه!", "تقویمت جا برای دورهمی داره، ولی وقتِ خودت هم از قلم نمی‌افته!" }),
                "JP" when outgoing => ("planning.structured", FactCategory.Planning, "planning.calendar", "🗓️", "نقشه‌کشِ ماجرا", "برای ماجراها یک نقشه هم دارم", new[] { "قبل از شروع ماجرا، یک نقشه کوچیک توی جیبت داری؛ غافلگیری با هماهنگی قبلی!", "برنامه داشتن برات یک جور آسودگیه؛ تیک زدن هم جایزهٔ کوچیکشه!" }),
                "JP" when quiet => ("planning.flexible", FactCategory.Planning, "adventure.rocket", "🚀", "جا برای غافلگیری", "برای تصمیم‌های در مسیر جا دارم", new[] { "برنامه‌ات برای اتفاق‌های وسط راه هم جا نگه می‌داره؛ مسیر خودش بخشی از ماجراست!", "همه‌چیز لازم نیست از قبل قفل بشه؛ کمی جا برای «حالا ببینیم چی می‌شه» داری!" }),
                "JP" => ("planning.balance", FactCategory.Planning, "planning.calendar", "🗓️", "نقشه با جای تغییر", "یک نقشه دارم، با حقِ تغییر مسیر", new[] { "یک برنامه خوب می‌خوای، با دکمهٔ ویرایشِ روشن!", "هم نقشه به کارت میاد، هم جا گذاشتن برای پیچِ بعدیِ مسیر!" }),
                _ when outgoing => ("adventure.new", FactCategory.Adventure, "adventure.rocket", "🚀", "شکارچی تجربه‌ها", "تجربهٔ تازه؟ ارزش امتحان داره", new[] { "گزینهٔ «امتحانش کنیم» توی چند انتخابت جلو افتاده؛ مأموریت فرعی پیدا شد!", "توی چند انتخاب، تجربهٔ تازه بیشتر وسوسه‌ات کرده؛ کنجکاوی بلیت می‌خواد!" }),
                _ => ("adventure.familiar", FactCategory.Adventure, "lifestyle.home", "🏡", "بلدِ حال خوب", "گاهی نسخهٔ محبوب قبلی بهترینه", new[] { "وقتی یک انتخاب امتحانش رو پس داده، چرا دوباره سراغش نری؟ نسخهٔ محبوبت رو بلدی!", "تجربه‌های آشنا توی چند انتخابت جلو افتادن؛ حال خوب همیشه آدرس تازه نمی‌خواد!" })
            };
            var support = trait.Evidence.Where(e => known.ContainsKey(e.Key)).ToArray();
            if (support.Length < DiscoveryEngine.MinimumTraitAnswers) continue;
            result.Add(new(DiscoveryEngine.ResultId(Version + ":" + rule, support), title, copies[Variant(rule, support, copies.Length)],
                "برداشت احتمالی", Label(category), support.Length, trait.Percent, false, true, support,
                CategoryKey: category, VisualKey: visual, Emoji: emoji, Certainty: FactCertainty.SupportedPattern, RuleKey: rule, StoryText: shortText));
        }
        AnalysisFact? Rule(string key) => result.FirstOrDefault(f => f.RuleKey == key &&
            !((decisions ?? []).Any(d => d.ResultKey == f.Id && d.State is AnalysisDecisionState.Rejected or AnalysisDecisionState.Partial)));
        void Combine(string key, FactCategory category, string visual, string emoji, string title, string shortText,
            string positive, string[] texts, params AnalysisFact?[] sources)
        {
            if (sources.Any(s => s is null)) return;
            var support = sources.SelectMany(s => s!.Evidence).DistinctBy(e => e.Key).ToArray();
            result.Add(new(DiscoveryEngine.ResultId(Version + ":" + key, support), title, texts[Variant(key, support, texts.Length)],
                "از چند سرنخ", Label(category), support.Length, null, false, true, support,
                CategoryKey: category, VisualKey: visual, Emoji: emoji, Certainty: FactCertainty.SupportedPattern,
                RuleKey: key, StoryText: shortText, IsCombination: true, PositiveText: positive));
        }
        AnalysisFact? Direct(string key) => known.TryGetValue(key, out var answer) ? new("", "", "", "", "", 1, null, false, false, [answer]) : null;
        AnalysisFact? Night() => Is("energy", 3) ? Direct("energy") : null;
        var music = Rule("music.energy") ?? Rule("music.genre") ?? Rule("music.companion");
        var nightTheme = Rule("animals.cat") is not null ? FactCategory.Animals : Rule("gaming.main") is not null ? FactCategory.Gaming : FactCategory.Music;
        if (nightTheme == FactCategory.Animals) Combine("combo.cat-night", FactCategory.Animals, "animals.cat", "🐱", "گربهٔ شیفت شب", "تیمِ میو، با باتریِ شبانه!",
            "وقت و سلیقهٔ خودت رو می‌شناسی؛ شب‌ها بیشتر روی فرمی.",
            ["حیوان انتخابی‌ات گربه‌ست و باتری خودت شب‌ها پُرتره؛ شیفت شبِ تیم میو تکمیل شد!", "تیم میو رو انتخاب کردی، انرژی شبانه هم داری؛ ماه برای این ترکیب کم مونده کارت عضویت بده!"], Rule("animals.cat"), Night());
        if (nightTheme == FactCategory.Gaming) Combine("combo.gaming-night", FactCategory.Gaming, "gaming.controller", "🎮", "قهرمانِ شیفت شب", "شب، وقتِ یک مرحلهٔ تازه‌ست",
            "برای ساعت‌های پرانرژیت یک تفریح محبوب داری.",
            ["ساعت اوج انرژیت آخر شبه و بازی هم توی فهرستته؛ ماه نقش چراغِ مرحله رو گرفته!", "شب‌ها بیشتر روی فرمی، بازی هم انتخابته؛ مرحلهٔ بعد با نور ماه شروع می‌شه!"], Rule("gaming.main"), Night());
        if (nightTheme == FactCategory.Music) Combine("combo.music-night", FactCategory.Music, "music.headphones", "🎧", "دی‌جیِ شیفت شب", "شب روشن‌تره، وقتی موسیقی متن داره",
            "برای زمانِ پرانرژیت، سلیقهٔ موسیقی خودت رو داری.",
            ["انرژیت شب‌ها بیشتره و موسیقی هم کنارته؛ برای ماه یک موسیقی متن کم داشتیم!", "شب برای تو می‌تونه تیتراژ داشته باشه؛ انتخاب آهنگ هم دست خودته!"], music, Night());
        Combine("combo.books-quiet", FactCategory.Books, "books.stack", "📚", "مسافرِ بی‌سروصدا", "خلوت من، دروازهٔ چند دنیای دیگه‌ست",
            "برای وقت شخصی و سرگرمیِ دلخواهت جا نگه می‌داری.",
            ["برای شارژ شدن خلوت رو دوست داری، کتاب هم همسفرته؛ سفر بدون صفِ فرودگاه پیدا شد!", "یک کتاب و کمی خلوت؛ برای رفتن به دنیای بعدی، پاسپورت لازم نداری!"], Rule("books.main"), Rule("social.quiet"));
        Combine("combo.music-focus", FactCategory.Music, "music.headphones", "🎧", "معمارِ موسیقی متن", "برای تمرکز، موسیقی متن خودم رو دارم",
            "برای فضای تمرکزت سلیقه و انتخاب مشخص داری.",
            ["موسیقی رو برای تمرکز انتخاب می‌کنی و برنامه هم بهت می‌چسبه؛ میز کارت فقط موسیقی تیتراژ کم داشت!", "برنامهٔ مشخص و موسیقیِ موقع تمرکز؛ انگار برای کارهات اتاق فرمانِ کوچیک ساختی!"], Is("music-context", 0) ? music : null, Direct("music-context"), Rule("planning.structured"));
        Combine("combo.music-road", FactCategory.Music, "music.headphones", "🎧", "دی‌جیِ جاده‌ها", "مسیر خوب، موسیقی متنِ خودم رو داره",
            "برای مسیر و مقصد، سلیقهٔ مشخص خودت رو داری.",
            ["موسیقیِ مسیر رو دوست داری، مقصد محبوب هم داری؛ فقط کافیه چمدون با ریتم پلی‌لیست بسته بشه!", "پلی‌لیستِ مسیر یک طرف، مقصدِ دلخواه یک طرف؛ سفر برای تو می‌تونه تیتراژ اختصاصی داشته باشه!"], Is("music-context", 2) ? music : null, Direct("music-context"), Rule("travel.nature") ?? Rule("travel.escape"));
        Combine("combo.quiet-energy", FactCategory.Music, "music.headphones", "🎧", "خلوتِ پُرصدا", "جمع خلوت‌تر، پلی‌لیست پرانرژی‌تر!",
            "خلوتِ دلخواهت و موسیقیِ پرانرژی، هر دو توی انتخاب‌هات جا دارن.",
            ["برای شارژ، خلوت رو انتخاب می‌کنی؛ برای آهنگ، انرژی رو! آرامشِ بیرون با کنسرتِ توی هدفون جمع می‌شه!", "خلوت رو دوست داری، ولی پلی‌لیستت آروم نمی‌شینه؛ اتاق ساکته، کنسرت توی هدفونه!"], Rule("music.energy"), Rule("social.quiet"));
        Combine("combo.party-plan", FactCategory.Social, "social.party", "🎉", "نقشه‌کشِ دورهمی‌ها", "جمع خوب، با یک نقشهٔ کوچیک",
            "برای معاشرت انرژی داری و از برنامهٔ روشن هم خوشت می‌آد.",
            ["جمع خوب شارژت می‌کنه، برنامه هم دوست داری؛ دورهمی‌ات احتمالاً حتی برای غافلگیری جای خالی داره!", "انرژی جمع رو دوست داری، نقشه هم توی جیبه؛ دورهمی با گزینهٔ «از قبل هماهنگ شده»!"], Rule("social.party"), Rule("planning.structured"));
        Combine("combo.adventure-flex", FactCategory.Adventure, "adventure.rocket", "🚀", "ماجراجوی دقیقه نودی", "تجربهٔ تازه، با جای خالی برای تغییر مسیر",
            "کنجکاوی و انعطاف، توی چند انتخابت کنار هم دیده می‌شن.",
            ["هم تجربهٔ تازه وسوسه‌ات می‌کنه، هم برنامهٔ شناور؛ مقصد معلوم نیست، ولی داستانش شنیدنیه!", "توی چند انتخاب، هم «امتحانش کنیم» گفتی هم جا برای تغییر گذاشتی؛ مأموریت فرعی رسماً بازه!"], Rule("adventure.new"), Rule("planning.flexible"));
        // Combinations replace their overlapping single-theme fact, keeping one short page.
        return result.OrderByDescending(f => f.IsCombination).ThenByDescending(f => f.Certainty == FactCertainty.SupportedPattern).ThenByDescending(f => f.SampleCount)
            .ThenBy(f => f.CategoryKey).GroupBy(f => f.CategoryKey).Select(g => g.First()).Take(MaxFacts).ToArray();
    }

    public static PersonalityPortrait Portrait(IReadOnlyList<AnalysisFact> facts, IReadOnlyList<TraitEstimate>? traits = null)
    {
        var active = facts.Where(f => !f.Sensitive && f.Decision is not (AnalysisDecisionState.Rejected or AnalysisDecisionState.Partial)).ToArray();
        bool Has(string rule) => active.Any(f => f.RuleKey == rule);
        var first = active.FirstOrDefault();
        var combined = active.FirstOrDefault(f => f.IsCombination);
        var (name, description, emoji, visual) = (Has("gaming.main"), Has("planning.flexible"), Has("social.quiet")) switch
        {
            (true, true, _) => ("سلطان «یه دست دیگه»", "یک بازی خوب، کمی وقت آزاد و جا برای تصمیم‌های وسط راه؛ برنامهٔ تو شروع شد!", "🎮", "gaming.controller"),
            (true, _, true) => ("قهرمانِ مرحله‌های خلوت", "یک گوشهٔ دنج و یک بازی خوب؛ برای ماجراجویی همیشه لازم نیست از خونه بزنی بیرون!", "🎮", "gaming.controller"),
            _ when Has("planning.structured") && Has("adventure.new") => ("ماجراجوی نقشه‌به‌دست", "تجربهٔ تازه رو دوست داری؛ با یک نقشهٔ کوچیک برای مسیر. کنجکاوی، با برنامه!", "🚀", "adventure.rocket"),
            _ when first is not null => (active.Length == 1 && first.CategoryKey == FactCategory.Music ? "یک سرنخِ هدفونی" : first.Title, first.StoryText, first.Emoji, first.VisualKey),
            _ => ("هنوز داریم می‌شناسیمت", "چند انتخاب تازه کمک می‌کنه تصویر روشن‌تری ازت پیدا کنیم؛ لقب الکی در کار نیست!", "✨", "spark")
        };
        if (combined is not null) (name, description, emoji, visual) = (combined.Title, combined.PositiveText, combined.Emoji, combined.VisualKey);
        var strengths = active.Select(f => f.RuleKey switch
        {
            "social.quiet" or "combo.books-quiet" or "combo.quiet-energy" => "برای وقت خودت جا داری",
            "social.party" or "combo.party-plan" => "از جمعِ خوب انرژی می‌گیری",
            "social.balance" => "هم جمع، هم خلوتِ به‌موقع",
            "planning.structured" or "combo.music-focus" => "با نقشهٔ روشن راحت‌تری",
            "planning.flexible" or "combo.adventure-flex" => "برای تغییر مسیر جا داری",
            "adventure.new" => "کنجکاوِ تجربه‌های تازه",
            "combo.cat-night" or "combo.gaming-night" or "combo.music-night" => "وقتِ روی فرمت رو می‌شناسی",
            "combo.music-road" => "برای سفر یک همراهِ آهنگی داری", _ => ""
        }).Where(t => t.Length > 0).Distinct().Take(2).ToArray();
        var interests = active.Where(f => f.CategoryKey is not (FactCategory.Social or FactCategory.Planning or FactCategory.Adventure))
            .Select(f => f.Title).Distinct().Take(3).ToArray();
        var unknowns = (traits ?? []).Where(t => (t.Key is "EI" or "JP" or "novelty") && !t.Percent.HasValue)
            .Take(2).Select(t => t.Key == "EI" ? "حال‌وهوای جمع و خلوتت هنوز روشن نیست." : t.Key == "JP" ? "سبک برنامه‌هات هنوز روشن نیست." : "دربارهٔ تجربه‌های تازه هنوز کمتر می‌دونیم.").ToArray();
        return new(name, description, emoji, visual, first is null ? "هنوز سرنخ کافی نداریم" :
            active.Length < 3 ? "یک تصویر اولیه؛ هنوز جا برای شناختن داریم" : "از چند انتخاب واقعی؛ یک برداشت سرگرم‌کننده، نه حکم قطعی",
            PositiveLine, unknowns, strengths, interests, combined?.Text ?? first?.Text ?? "");
    }

    public static StoryContent? Story(string owner, IReadOnlyList<AnalysisFact> facts, IEnumerable<string>? selected = null)
    {
        var ids = selected?.ToHashSet(StringComparer.Ordinal);
        var safe = facts.Where(f => !f.Sensitive && f.CanShare && f.Decision == AnalysisDecisionState.Confirmed &&
            !string.IsNullOrWhiteSpace(f.RuleKey) && f.CategoryKey != FactCategory.Other && (ids is null || ids.Contains(f.Id))).Take(4).ToArray();
        if (safe.Length == 0) return null;
        var portrait = Portrait(safe);
        return new("personality", owner, portrait.Nickname, portrait.Description, portrait.Emoji, portrait.VisualKey,
            portrait.PositiveLine, "تو هم نسخهٔ غیررسمی خودت رو کشف کن!", safe.Select(StoryFact.From).ToArray());
    }
    private static int Variant(string rule, IEnumerable<AnswerEvidence> evidence, int length) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(rule + ":" + string.Join("|", evidence.OrderBy(e => e.Key).Select(e => e.Key + ":" + e.Value))))[0] % length;
    public static string Label(FactCategory category) => category switch
    { FactCategory.Music => "موسیقی", FactCategory.Gaming => "بازی", FactCategory.Film => "فیلم و سریال", FactCategory.Books => "کتاب", FactCategory.Animals => "حیوانات", FactCategory.Travel => "سفر", FactCategory.Food => "خوشمزه‌ها", FactCategory.Sport => "ورزش", FactCategory.Style => "استایل", FactCategory.Social => "جمع و خلوت", FactCategory.Planning => "برنامه‌های تو", FactCategory.Adventure => "ماجراجویی", _ => "حال‌وهوای تو" };
}
