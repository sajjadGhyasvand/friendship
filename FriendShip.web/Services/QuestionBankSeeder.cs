using FriendShip.Application.Abstractions;
using FriendShip.Domain.Questions;
using Microsoft.EntityFrameworkCore;
using FriendShip.Application.Questions;

namespace FriendShip.Web.Services;

public static class QuestionBankSeeder
{
    public static async Task SeedAsync(
        IAppDbContextFactory dbFactory)
    {
        await using var db = dbFactory.Create();

        const string categoryName = "شناخت دوست";

        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.Name == categoryName);

        if (category is null)
        {
            category = Category.Create(
                categoryName,
                "🤝",
                sortOrder: 1);

            db.Categories.Add(category);

            // ابتدا شناسه‌ی دسته‌بندی از دیتابیس دریافت شود.
            await db.SaveChangesAsync();
        }

        // دسته‌ی موجود و غیرفعال را خودکار فعال نمی‌کنیم.
        if (!category.IsActive)
        {
            await SeedPersonalTemplatesAsync(dbFactory);
            return;
        }

        var samples = new (string Text, string[] Options)[]
        {
            (
                "من کدام فصل سال را بیشتر دوست دارم؟",
                new[] { "بهار", "تابستان", "پاییز", "زمستان" }
            ),
            (
                "برای سفر بیشتر کدام مقصد را انتخاب می‌کنم؟",
                new[] { "دریا", "کوهستان", "جنگل", "شهر تاریخی" }
            ),
            (
                "در وقت آزاد بیشتر دوست دارم چه کار کنم؟",
                new[] { "فیلم دیدن", "کتاب خواندن", "بازی کردن", "ورزش کردن" }
            ),
            (
                "کدام نوشیدنی را بیشتر دوست دارم؟",
                new[] { "چای", "قهوه", "آبمیوه", "آب" }
            ),
            (
                "معمولاً چه زمانی از روز انرژی بیشتری دارم؟",
                new[] { "صبح زود", "ظهر", "عصر", "آخر شب" }
            ),
            (
                "کدام نوع فیلم را بیشتر دوست دارم؟",
                new[] { "کمدی", "ماجراجویی", "معمایی", "درام" }
            ),
            (
                "برای یک دورهمی کدام فضا را ترجیح می‌دهم؟",
                new[] { "خانه", "کافه", "پارک", "رستوران" }
            ),
            (
                "کدام هدیه بیشتر خوشحالم می‌کند؟",
                new[] { "کتاب", "وسیله‌ی کاربردی", "یادگاری دست‌ساز", "بلیط سفر" }
            ),
            (
                "وقتی ناراحتم بیشتر به چه چیزی نیاز دارم؟",
                new[] { "کمی تنهایی", "صحبت با یک دوست", "قدم زدن", "گوش دادن به موسیقی" }
            ),
            (
                "برای برنامه‌ریزی سفر معمولاً چه روشی دارم؟",
                new[] { "برنامه‌ی دقیق", "تصمیم لحظه‌ای", "سپردن به دوست‌ها", "ترکیبی از این‌ها" }
            )
        };

        var existingTexts = await db.Questions
            .Where(q => q.CategoryId == category.Id)
            .Select(q => q.Text)
            .ToListAsync();

        var knownTexts = existingTexts.ToHashSet(
            StringComparer.Ordinal);

        var now = DateTime.UtcNow;

        foreach (var sample in samples)
        {
            if (!knownTexts.Add(sample.Text))
                continue;

            var question = Question.Create(
                category,
                sample.Text,
                sample.Options,
                now);

            db.Questions.Add(question);
        }

        await db.SaveChangesAsync();
        await SeedPersonalTemplatesAsync(dbFactory);
    }

    private static async Task SeedPersonalTemplatesAsync(IAppDbContextFactory dbFactory)
    {
        await using var db = dbFactory.Create();
        foreach (var group in QuestionFactory.Catalog.GroupBy(t => new { t.Category, t.Icon }))
        {
            var category = await db.Categories.FirstOrDefaultAsync(c => c.Name == group.Key.Category);
            if (category is null)
            {
                category = Category.Create(group.Key.Category, group.Key.Icon, 10);
                db.Categories.Add(category);
                await db.SaveChangesAsync();
            }
            foreach (var template in group)
            {
                if (!await db.Questions.AnyAsync(q => q.Text == template.Prompt))
                    db.Questions.Add(Question.Create(category, template.Prompt, template.Options, DateTime.UtcNow));
            }
            await db.SaveChangesAsync();
        }
    }
}
