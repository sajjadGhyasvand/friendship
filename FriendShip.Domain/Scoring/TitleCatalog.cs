namespace FriendShip.Domain.Scoring;

public sealed record TierInfo(ScoreTier Tier, string Title, string Emoji, string Message);

public static class TitleCatalog
{
    public static TierInfo For(ScoreTier tier) => tier switch
    {
        ScoreTier.SoulMate => new(tier, "رفیق همه‌چیزدان", "🏆", "تمام انتخاب‌ها رو درست حدس زدی!"),
        ScoreTier.BestFriend => new(tier, "ذهن‌خوان حرفه‌ای", "✦", "تقریباً همه انتخاب‌ها رو درست حدس زدی."),
        ScoreTier.GoodFriend => new(tier, "رفیق خوب", "😎", "خوب منو می‌شناسی، ولی هنوز جای پیشرفت هست."),
        ScoreTier.Friend => new(tier, "دوست", "🙂", "یه چیزهایی می‌دونی، بیشتر باهم حرف بزنیم!"),
        ScoreTier.Acquaintance => new(tier, "آشنا", "👋", "تازه شروع آشناییه."),
        _ => new(tier, "شروع یک کشف تازه", "🌱", "هنوز کلی انتخاب بامزه برای کشف کردن هست!")
    };

    public static TierInfo ForPercent(int percent) => For(ScoreCalculator.TierFor(percent));
}
