using System.Security.Cryptography;
using FriendShip.Domain.Common;
using FriendShip.Domain.Rooms;

namespace FriendShip.Application.Questions;

public sealed record ProfileTemplate(string Key, string Category, string Icon, string Prompt,
    string[] Options, string[] FriendPrompts, string Difficulty);

/// <summary>Internal templates contain no private data and require no external API.</summary>
public sealed class QuestionFactory
{
    public const string Version = "templates-v2";
    public static IReadOnlyList<ProfileTemplate> Catalog => CoreCatalog.Concat(FriendShip.Application.Discovery.DiscoveryCatalog.GameTemplates).ToArray();
    public static IReadOnlyList<ProfileTemplate> CoreCatalog { get; } = new ProfileTemplate[]
    {
        new("music", "موسیقی", "♫", "چه سبک موسیقی بیشتر گوش می‌دی؟",
            ["پاپ", "راک", "رپ", "کلاسیک"],
            ["اگر پلی‌لیست سفر دستش باشه، بیشتر چه سبکی می‌شنویم؟", "برای یک روز با هدفون، کدوم سبک رو انتخاب می‌کنه؟"], "آسان"),
        new("free-time", "سرگرمی", "🎮", "سرگرمی محبوبت چیه؟",
            ["بازی کامپیوتری", "فیلم و سریال", "کتاب خوندن", "ورزش"],
            ["یک روز بدون هیچ مسئولیتی؛ احتمالاً مشغول چیه؟", "وقتی برنامه‌ها کنسل می‌شن، وقت آزادش رو چطور پر می‌کنه؟"], "آسان"),
        new("weekend", "سبک زندگی", "☀", "تعطیلات رو کجا بیشتر دوست داری بگذرونی؟",
            ["خونه و استراحت", "بیرون با دوست‌ها", "طبیعت‌گردی", "گشت شهری"],
            ["یک جمعه کاملاً آزاد؛ کدوم برنامه بیشتر ذوق‌زده‌اش می‌کنه؟", "اگر آخر هفته فقط یک برنامه داشته باشه، کدومه؟"], "متوسط"),
        new("drink", "خوراکی", "☕", "نوشیدنی محبوبت چیه؟",
            ["قهوه", "چای", "آبمیوه", "شکلات داغ"],
            ["برای شروع یک روز شلوغ، کدوم نوشیدنی بیشتر بهش می‌چسبه؟", "توی کافه فقط یک نوشیدنی سفارش می‌ده؛ کدوم؟"], "آسان"),
        new("travel", "سفر", "✈", "برای سفر چه مقصدی رو ترجیح می‌دی؟",
            ["جنگل و طبیعت", "دریا و ساحل", "شهر تاریخی", "کوهستان"],
            ["یک بلیت برای همین آخر هفته؛ کجا دلش می‌خواد بره؟", "اگر چمدونش آماده باشه، به سمت کدوم مقصد راه می‌افته؟"], "متوسط"),
        new("movie", "سرگرمی", "🎮", "چه ژانر فیلمی رو بیشتر دوست داری؟",
            ["کمدی", "معمایی", "ماجراجویی", "درام"],
            ["انتخاب فیلم امشب با اونه؛ احتمالاً چه ژانری می‌بینیم؟", "کدوم ژانر باعث می‌شه بگه «فقط یک قسمت دیگه»؟"], "متوسط"),
        new("energy", "عادت‌ها", "◷", "چه زمانی از روز بیشتر انرژی داری؟",
            ["صبح زود", "ظهر", "عصر", "آخر شب"],
            ["برای انجام یک کار خلاقانه، کدوم ساعت بیشتر روی فرمه؟", "چه وقت احتمال بیشتری داره با انرژی سراغ کارها بره؟"], "غافلگیرکننده"),
        new("gift", "ترجیحات", "🎁", "چه نوع هدیه‌ای بیشتر خوشحالت می‌کنه؟",
            ["کتاب", "وسیله کاربردی", "یادگاری دست‌ساز", "یک تجربه تازه"],
            ["قرار باشه بی‌مناسبت غافلگیرش کنی، کدوم هدیه انتخاب بهتریه؟", "توی یک جعبه سورپرایز، دیدن کدوم بیشتر خوشحالش می‌کنه؟"], "غافلگیرکننده"),
        new("planning", "شخصیت", "✦", "برنامه‌ریزی رو دوست داری یا تصمیم لحظه‌ای؟",
            ["برنامه‌ریزی", "تصمیم لحظه‌ای"],
            ["برای یک سفر ناگهانی، اول برنامه می‌چینه یا همون لحظه راه می‌افته؟", "یک روز آزاد پیش روشه؛ کدوم سبک انتخاب بهش نزدیک‌تره؟"], "متوسط"),
        new("food", "خوراکی", "🍕", "از بین این غذاها کدوم رو ترجیح می‌دی؟",
            ["پیتزا", "پاستا", "کباب", "برگر"],
            ["همه منتظر انتخاب رستورانن؛ پیشنهاد اولش احتمالاً چیه؟", "برای شام جایزه بعد از یک روز سخت، کدوم رو انتخاب می‌کنه؟"], "آسان")
    };

    public void Generate(Room room, IReadOnlyDictionary<string, int> answers)
    {
        var usedPrompts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var question in room.Questions)
        {
            var template = Catalog.SingleOrDefault(t => t.Prompt == question.Text)
                ?? throw new DomainException("question.template", "برای این سؤال قالب مناسبی وجود ندارد.");
            Guard.Against(!answers.TryGetValue(template.Key, out var choice) || choice < 0 || choice >= template.Options.Length,
                "question.choice", "به سؤال‌های انتخاب‌شده پاسخ معتبر بده.");
            var correctText = template.Options[choice];
            Guard.Against(!question.Options.Select(o => o.Text).ToHashSet(StringComparer.Ordinal)
                .SetEquals(template.Options), "question.pool", "گزینه‌های این سؤال با قالب هماهنگ نیستند.");
            var correct = question.Options.SingleOrDefault(o => o.Text == correctText)
                ?? throw new DomainException("question.option", "گزینه مرجع در بانک سؤال وجود ندارد.");
            room.SetOwnerAnswer(question.Id, correct.Id);
            var prompt = template.FriendPrompts[RandomNumberGenerator.GetInt32(template.FriendPrompts.Length)];
            // Each category has distinct prompts; avoid a duplicate if templates are extended later.
            if (!usedPrompts.Add(prompt))
                prompt = template.FriendPrompts.FirstOrDefault(usedPrompts.Add)
                    ?? throw new DomainException("question.duplicate", "قالب تکراری است؛ سؤال دیگری انتخاب کن.");
            var order = question.Options.Select(o => o.Id).ToArray();
            for (var i = order.Length - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            room.GenerateQuestion(question.Id, template.Prompt, prompt, template.Category, template.Icon, order, Version);
            room.SetQuestionTemplate(question.Id, template.Key, FriendShip.Application.Discovery.DiscoveryQuestion.Version);
        }
    }
}
