using FriendShip.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Domain.Questions
{
    public class Question
    {
        public const int TextMin = 5;
        public const int TextMax = 200;
        public const int OptionsMin = 2;
        public const int OptionsMax = 6;
        public const int OptionTextMax = 80;

        public int Id { get; private set; }
        public int CategoryId { get; private set; }
        public Category Category { get; private set; } = default!;
        public string Text { get; private set; } = default!;
        public bool IsActive { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        private readonly List<QuestionOption> _options = new();
        public IReadOnlyList<QuestionOption> Options => _options;

        private Question() { }

        public static Question Create(Category category, string text, IEnumerable<string> options, DateTime now)
        {
            var q = new Question
            {
                Category = category,
                CategoryId = category.Id,
                Text = ValidText(text),
                IsActive = true,
                CreatedAt = now
            };
            var list = ValidOptions(options.ToList());
            Guard.Against(list.Count is < OptionsMin or > OptionsMax, "question.options",
                $"هر سؤال باید بین {OptionsMin} تا {OptionsMax} گزینه داشته باشد.");
            for (var i = 0; i < list.Count; i++)
                q._options.Add(new QuestionOption(list[i], i + 1));
            return q;
        }

        /// <summary>
        /// Edits wording. Option count is fixed. Existing rooms are unaffected because they hold snapshots.
        /// </summary>
        public void Edit(Category category, string text, IReadOnlyList<string> optionTexts, DateTime now)
        {
            Guard.Against(optionTexts.Count != _options.Count, "question.options.count", "تعداد گزینه‌ها قابل تغییر نیست.");
            var cleaned = ValidOptions(optionTexts.ToList());
            Text = ValidText(text);
            Category = category;
            CategoryId = category.Id;
            var ordered = _options.OrderBy(o => o.Order).ToList();
            for (var i = 0; i < ordered.Count; i++) ordered[i].Rename(cleaned[i]);
            UpdatedAt = now;
        }

        public void Activate() => IsActive = true;
        public void Deactivate() => IsActive = false;

        private static string ValidText(string? t) =>
            Guard.Text(t, TextMin, TextMax, "question.text", $"متن سؤال باید بین {TextMin} تا {TextMax} حرف باشد.");

        private static List<string> ValidOptions(List<string> raw)
        {
            var list = raw.Select(o => Guard.Text(o, 1, OptionTextMax, "question.option",
                $"متن هر گزینه باید بین ۱ تا {OptionTextMax} حرف باشد.")).ToList();
            Guard.Against(list.Distinct(StringComparer.Ordinal).Count() != list.Count,
                "question.options.duplicate", "گزینه‌های تکراری مجاز نیستند.");
            return list;
        }
    }

    public class QuestionOption
    {
        public int Id { get; private set; }
        public int QuestionId { get; private set; }
        public string Text { get; private set; } = default!;
        public int Order { get; private set; }

        private QuestionOption() { }

        internal QuestionOption(string text, int order) { Text = text; Order = order; }

        internal void Rename(string text) => Text = text;
    }
}
