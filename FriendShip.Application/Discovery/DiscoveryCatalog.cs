using FriendShip.Application.Questions;

namespace FriendShip.Application.Discovery;

public sealed record DiscoveryQuestion(string Key, string Category, string Icon, string Prompt, string[] Options,
    bool Direct = true, string? InterestKey = null,
    string? Dimension = null, int[]? Scores = null, bool Sensitive = false, bool MbtiOnly = false,
    string? FollowKey = null, int[]? FollowChoices = null, string? FriendPrompt = null, bool FreeText = false)
{
    public const string Version = "discovery-q1";
}

/// <summary>Each rule is explicit. Updating a meaning requires a new question key/version.</summary>
public static class DiscoveryCatalog
{
    public static IReadOnlyList<DiscoveryQuestion> All { get; } = Build();
    public static DiscoveryQuestion? Find(string key) => All.FirstOrDefault(q => q.Key == key);
    public static IEnumerable<ProfileTemplate> GameTemplates => All.Where(q => q.FriendPrompt is not null && !q.Sensitive && !q.MbtiOnly && !q.FreeText && q.Options.Length <= 6)
        .Select(q => new ProfileTemplate(q.Key, q.Category, q.Icon, q.Prompt, q.Options, [q.FriendPrompt!], "موقعیتی"));

    private static IReadOnlyList<DiscoveryQuestion> Build()
    {
        var core = QuestionFactory.CoreCatalog.Select(t => new DiscoveryQuestion(t.Key, t.Category, t.Icon, t.Prompt, t.Options,
            InterestKey: t.Key switch { "music" => "music.genre", "movie" => "screen.genre", "free-time" => "hobby.main", "planning" => null, _ => "preference." + t.Key },
            Dimension: t.Key == "planning" ? "JP" : null, Scores: t.Key == "planning" ? [100, -100] : null)).ToList();
        core.AddRange(new DiscoveryQuestion[]
        {
            new("music-region", "موسیقی", "♫", "برای یک مسیر طولانی، پلی‌لیست کدوم فضا رو انتخاب می‌کنی؟", ["ایرانی", "بین‌المللی", "ترکیبی", "بیشتر بی‌کلام"], InterestKey: "music.region", FriendPrompt: "برای مسیر طولانی، پلی‌لیستش بیشتر چه فضایی داره؟"),
            new("music-mood", "موسیقی", "♫", "وقتی می‌خوای انرژی بگیری، کدوم پلی‌لیست رو می‌ذاری؟", ["پرانرژی و تند", "آرام و ملایم", "احساسی و غمگین", "شاد و رقص‌آور"], InterestKey: "music.mood", FriendPrompt: "وقتی می‌خواد حال‌وهوایش عوض بشه، کدوم پلی‌لیست بهش نزدیک‌تره؟"),
            new("music-context", "موسیقی", "♫", "معمولاً کِی بیشتر سراغ موسیقی می‌ری؟", ["مطالعه و تمرکز", "ورزش و حرکت", "مسیر و رانندگی", "استراحت و خلوت"], InterestKey: "music.context", FriendPrompt: "کدوم موقعیت بیشتر باعث می‌شه هدفونش رو برداره؟"),
            new("music-era", "موسیقی", "♫", "برای پلی‌لیست تازه، کدوم انتخاب بیشتر وسوسه‌ات می‌کنه؟", ["آهنگ‌های نوستالژیک", "انتشارهای تازه", "هر دو", "کشف هنرمند کمترشناخته‌شده"], InterestKey: "music.era"),
            new("music-detail", "موسیقی", "♫", "از بین این سبک‌ها، برای یک کنسرت کدوم رو انتخاب می‌کنی؟", ["متال", "الکترونیک", "هیپ‌هاپ", "آلترناتیو"], InterestKey: "music.concert", FollowKey: "music", FollowChoices: [1,2]),
            new("music-artist", "موسیقی", "♫", "اگر دوست داری، نام یک هنرمند محبوبت رو خودت بنویس", [], InterestKey: "music.artist", FreeText: true),
            new("style-main", "استایل", "✧", "یک روز آزاد داری؛ کدوم استایل رو انتخاب می‌کنی؟", ["کژوال", "مینیمال", "اسپرت", "استریت‌ویر", "کلاسیک", "وینتیج", "رسمی", "آلت", "ترکیبی متناسب با موقعیت"], InterestKey: "style.main", FriendPrompt: "برای یک روز آزاد، کدوم استایل بیشتر شبیه اونه؟"),
            new("style-priority", "استایل", "✧", "دو لباس دوست‌داشتنی دیدی؛ کدوم عامل انتخابت رو نهایی می‌کنه؟", ["راحتی", "ظاهر متفاوت", "هماهنگی رنگ‌ها", "کیفیت و دوام"], InterestKey: "style.priority", FriendPrompt: "بین دو لباس، چه چیزی بیشتر انتخابش رو تعیین می‌کنه؟"),
            new("style-context", "استایل", "✧", "برای یک دورهمی معمولی کدوم رو برمی‌داری؟", ["لباس راحت همیشگی", "یک ترکیب جدید و خاص", "ساده با رنگ‌های هماهنگ", "مرتب و کلاسیک"], Direct: false, InterestKey: "style.social"),
            new("style-colors", "استایل", "✧", "برای لباس تازه بیشتر سراغ کدوم پالت می‌ری؟", ["خنثی و ساده", "روشن و پرانرژی", "تیره و یکدست", "ترکیبی با یک رنگ خاص"], InterestKey: "style.colors", FriendPrompt: "برای یک لباس تازه، کدوم ترکیب رنگ بیشتر چشمش رو می‌گیره؟"),
            new("daily-space", "عادت‌ها", "◷", "برای استراحت کوتاه، چه فضایی بیشتر بهت می‌چسبه؟", ["آرام و کم‌نور", "روشن و کنار پنجره", "با موسیقی پس‌زمینه", "بیرون از خونه"], InterestKey: "habits.rest", FriendPrompt: "برای یک استراحت کوتاه، چه فضایی بیشتر بهش می‌چسبه؟"),
            new("social-weekend", "رفتار اجتماعی", "☀", "بعد از یک هفته شلوغ، کدوم برنامه بیشتر شارژت می‌کنه؟", ["جمع شلوغ دوست‌ها", "یک یا دو دوست نزدیک", "خلوت و سرگرمی شخصی", "کشف یک جای تازه به‌تنهایی"], Direct: false, Dimension: "EI", Scores: [100,25,-100,-50], FriendPrompt: "بعد از یک هفته شلوغ، کدوم برنامه بیشتر شارژش می‌کنه؟"),
            new("social-party", "رفتار اجتماعی", "✦", "در یک مهمانی ناآشنا، معمولاً چطور شروع می‌کنی؟", ["خودم سر صحبت رو باز می‌کنم", "اول نگاه می‌کنم، بعد وارد گفتگو می‌شم", "کنار یک آشنا می‌مونم", "به یک گفتگوی دونفره ملحق می‌شم"], Direct: false, Dimension: "EI", Scores: [100,0,-100,-25]),
            new("social-recharge", "رفتار اجتماعی", "☀", "یک روز پر از گفتگو تموم شده؛ حالا چی می‌چسبه؟", ["یک دورهمی دیگه", "چند ساعت خلوت", "کمی گفتگو و کمی تنهایی", "یک پیاده‌روی مستقل"], Direct: false, Dimension: "EI", Scores: [100,-100,0,-50]),
            new("plan-trip", "تصمیم‌گیری", "✦", "سفر فرداست و برنامه خالیه؛ چه کار می‌کنی؟", ["برنامه مسیر و زمان رو می‌نویسم", "فقط چند نقطه اصلی رو مشخص می‌کنم", "می‌رم ببینم چی پیش میاد", "برنامه شناور می‌چینم"], Direct: false, Dimension: "JP", Scores: [100,40,-100,-40]),
            new("plan-project", "تصمیم‌گیری", "✦", "برای یک کار شخصی تازه، کدوم روش بهت نزدیک‌تره؟", ["مرحله‌ها و زمان مشخص", "شروع و اصلاح در مسیر", "چارچوب مشخص با جای تغییر", "چند مسیر باز تا لحظه آخر"], Direct: false, Dimension: "JP", Scores: [100,-100,40,-50]),
            new("detail-learning", "نگاه به تجربه‌ها", "◈", "یک مهارت تازه یاد می‌گیری؛ اول کدوم به کارت میاد؟", ["مثال واقعی و قدم‌های مشخص", "ایده کلی و امکان‌های آینده", "امتحان عملی یک نمونه", "وصل کردنش به چند ایده دیگر"], Direct: false, Dimension: "SN", Scores: [100,-100,70,-70], MbtiOnly: true),
            new("detail-story", "نگاه به تجربه‌ها", "◈", "از یک سفر تعریف می‌کنی؛ بیشتر از چی می‌گی؟", ["اتفاق‌ها و جزئیات واقعی", "برداشت‌ها و معناهایی که پیدا کردم", "چیزهایی که دقیق دیدم", "ایده‌هایی که برای سفر بعد دارم"], Direct: false, Dimension: "SN", Scores: [100,-100,70,-70], MbtiOnly: true),
            new("detail-new", "نگاه به تجربه‌ها", "◈", "یک وسیله جدید بهت می‌دن؛ اول چی رو بررسی می‌کنی؟", ["روش استفاده و کاربرد فعلی", "کارهای تازه‌ای که می‌شه باهاش کرد", "تجربه‌های عملی دیگران", "امکان ترکیبش با ایده‌های دیگر"], Direct: false, Dimension: "SN", Scores: [100,-100,70,-70], MbtiOnly: true),
            new("decision-group", "سبک تصمیم", "◇", "گروه بین دو برنامه گیر کرده؛ اول چه چیزی رو می‌سنجی؟", ["مزایا و محدودیت‌های هر برنامه", "اینکه هر کس با کدوم راحت‌تره", "معیار یکسان برای مقایسه", "احساس و نیاز افراد جمع"], Direct: false, Dimension: "TF", Scores: [100,-100,70,-70], MbtiOnly: true),
            new("decision-feedback", "سبک تصمیم", "◇", "دوستت برای یک کار بازخورد می‌خواد؛ از کجا شروع می‌کنی؟", ["نقاط قابل اصلاح با دلیل روشن", "احساسی که از کارش گرفتم", "معیارهای کار و نتیجه", "تلاش و انگیزه پشت کار"], Direct: false, Dimension: "TF", Scores: [100,-100,70,-70], MbtiOnly: true),
            new("decision-choice", "سبک تصمیم", "◇", "دو انتخاب نزدیک داری؛ کدوم بیشتر کمکت می‌کنه؟", ["مقایسه منطقی پیامدها", "هماهنگی با ارزش‌های شخصی", "معیار قابل اندازه‌گیری", "اثر انتخاب روی آدم‌های اطرافم"], Direct: false, Dimension: "TF", Scores: [100,-100,70,-70], MbtiOnly: true),
            new("novelty-place", "تجربه‌های تازه", "✈", "برای بیرون رفتن، این بار کدوم رو انتخاب می‌کنی؟", ["جای آشنای محبوب", "یک جای کاملاً تازه", "جای آشنا با یک تغییر کوچک", "هر بار یک تجربه متفاوت"], Direct: false, Dimension: "novelty", Scores: [-100,100,-30,70], FriendPrompt: "برای بیرون رفتن، کدوم انتخاب بیشتر به سلیقه‌اش می‌خوره؟"),
            new("novelty-food", "تجربه‌های تازه", "✈", "منوی تازه‌ای جلوت هست؛ معمولاً چی سفارش می‌دی؟", ["غذای همیشگی", "چیزی که هیچ‌وقت نخوردم", "پیشنهاد نزدیک به سلیقه‌ام", "پیشنهاد متفاوت سرآشپز"], Direct: false, Dimension: "novelty", Scores: [-100,100,-30,70]),
            new("novelty-hobby", "تجربه‌های تازه", "✈", "یک عصر وقت اضافه پیدا کردی؛ چه انتخابی وسوسه‌ات می‌کنه؟", ["سرگرمی همیشگی", "امتحان یک سرگرمی تازه", "نسخه تازه‌ای از سرگرمی قبلی", "ثبت‌نام یک تجربه ناشناخته"], Direct: false, Dimension: "novelty", Scores: [-100,100,20,70]),
            new("game-style", "بازی و دیجیتال", "🎮", "یک بازی تازه نصب می‌کنی؛ کدوم تجربه رو می‌خوای؟", ["داستانی و تک‌نفره", "رقابتی آنلاین", "همکاری با دوست‌ها", "معمایی و آرام"], InterestKey: "games.style", FollowKey: "free-time", FollowChoices: [0], FriendPrompt: "کدوم نوع بازی بیشتر ساعت‌ها سرگرمش می‌کنه؟"),
            new("screen-world", "فیلم و انیمه", "▶", "برای یک شب تماشا، کدوم رو بیشتر انتخاب می‌کنی؟", ["فیلم سینمایی", "سریال", "انیمه", "مستند"], InterestKey: "screen.format", FriendPrompt: "یک شب آزاد برای تماشا؛ انتخاب اولش کدومه؟"),
            new("book-genre", "کتاب", "▤", "جلوی قفسه کتاب، کدوم بخش بیشتر نگهت می‌داره؟", ["داستان و رمان", "فانتزی و علمی‌تخیلی", "تاریخ و زندگی‌نامه", "مهارت و یادگیری"], InterestKey: "books.genre", FollowKey: "free-time", FollowChoices: [2], FriendPrompt: "توی کتاب‌فروشی احتمالاً بیشتر جلوی کدوم قفسه می‌ایسته؟"),
            new("sport-style", "ورزش", "◉", "برای تحرک و حال بهتر، کدوم رو ترجیح می‌دی؟", ["پیاده‌روی", "ورزش تیمی", "تمرین مستقل", "رقص و حرکت با موسیقی"], InterestKey: "sport.activity", FriendPrompt: "برای یک برنامه پرتحرک، کدوم انتخاب بیشتر خوشحالش می‌کنه؟"),
            new("digital-break", "بازی و دیجیتال", "▣", "ده دقیقه استراحت با گوشی؛ بیشتر سراغ چی می‌ری؟", ["ویدئوی کوتاه", "گفتگو با دوست‌ها", "موسیقی و پادکست", "مطالعه و کشف ایده"], InterestKey: "digital.break", FriendPrompt: "توی یک استراحت کوتاه با گوشی، احتمالاً مشغول چیه؟"),
            new("animal", "حیوانات", "♧", "برای یک روز وقت گذروندن با حیوانات، کدوم رو انتخاب می‌کنی؟", ["گربه", "سگ", "پرنده", "تماشای حیات‌وحش"], InterestKey: "animals.preference"),
            new("art", "هنر", "✎", "یک کارگاه هنری رایگان دعوتت کرده؛ کدوم رو انتخاب می‌کنی؟", ["نقاشی و طراحی", "عکاسی", "موسیقی", "ساختن با دست"], InterestKey: "art.activity"),
            new("celebration", "دوستی و مناسبت‌ها", "🎁", "تولد ایده‌آلت چه شکلیه؟", ["جمع کوچک و صمیمی", "مهمانی شلوغ", "یک تجربه تازه", "روز آرام برای خودم"], InterestKey: "celebration.style", FriendPrompt: "برای تولدش کدوم برنامه بیشتر به دلش می‌شینه؟"),
            new("future", "آینده و آرزوها", "✧", "اگر یک ماه فرصت آزاد داشتی، بیشتر روی چی وقت می‌ذاشتی؟", ["یادگیری مهارت", "سفر و کشف", "ساختن یک پروژه", "هنر و خلاقیت"], InterestKey: "future.focus"),
            new("friend-conflict", "دوستی و گفتگو", "♡", "از دوستت دلخوری؛ کدوم واکنش بهت نزدیک‌تره؟", ["گفتگوی مستقیم و آرام", "کمی فاصله و بعد گفتگو", "اول مرتب کردن فکرهام", "مشورت با یک دوست مطمئن"], Direct: false, InterestKey: "friendship.conflict"),
            new("fun-habit", "عادت‌های بامزه", "✦", "یک آهنگ محبوب پخش می‌شه؛ احتمالاً چه می‌کنی؟", ["باهاش می‌خونم", "ریتم می‌گیرم", "فقط گوش می‌دم", "اسمش رو برای بعد ذخیره می‌کنم"], Direct: false, InterestKey: "habits.music"),
            new("romance-message", "روابط اختیاری", "♡", "در یک موقعیت فرضی، کسی که دوستش داری پیام می‌ده؛ واکنشت؟", ["همون لحظه جواب می‌دم", "اول کمی فکر می‌کنم", "با شوخی شروع می‌کنم", "ساده و آرام جواب می‌دم"], Direct: false, InterestKey: "private.message", Sensitive: true),
            new("romance-value", "روابط اختیاری", "♡", "در یک رابطه فرضی کدوم برایت مهم‌تره؟", ["وقت گذروندن با هم", "گفتگوی صمیمانه", "حمایت متقابل", "تجربه‌های تازه"], InterestKey: "private.value", Sensitive: true),
            new("romance-attention", "روابط اختیاری", "♡", "کدوم رفتار ممکنه در نگاه اول توجهت رو جلب کنه؟", ["شوخ‌طبعی", "توجه به جزئیات", "اعتمادبه‌نفس", "مهربانی"], InterestKey: "private.attention", Sensitive: true)
        });
        return core;
    }
}
