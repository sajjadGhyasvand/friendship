using FriendShip.Domain.Common;
using FriendShip.Domain.Questions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Domain.Rooms
{
    public class RoomQuestion
    {
        public Guid Id { get; private set; }
        public Guid RoomId { get; private set; }
        public int SourceQuestionId { get; private set; }
        public int Order { get; private set; }
        public string Text { get; private set; } = default!;
        public string? OwnerPrompt { get; private set; }
        public string? GeneratorVersion { get; private set; }
        public string? TemplateKey { get; private set; }
        public string? TemplateVersion { get; private set; }
        public string CategoryName { get; private set; } = default!;
        public string CategoryIcon { get; private set; } = default!;

        /// <summary>The owner's answer. Never sent to a guest until reveal is allowed.</summary>
        public Guid? OwnerAnswerOptionId { get; private set; }

        private readonly List<RoomQuestionOption> _options = new();
        public IReadOnlyList<RoomQuestionOption> Options => _options;

        public bool IsAnsweredByOwner => OwnerAnswerOptionId.HasValue;

        private RoomQuestion() { }

        internal RoomQuestion(Guid roomId, int order, Question source)
        {
            Id = Guid.NewGuid();
            RoomId = roomId;
            Order = order;
            CopyFrom(source);
        }

        internal void ReplaceSource(Question source)
        {
            _options.Clear();
            OwnerAnswerOptionId = null;
            OwnerPrompt = null; GeneratorVersion = null; TemplateKey = null; TemplateVersion = null;
            CopyFrom(source);
        }

        internal void SetTemplate(string key, string version)
        {
            TemplateKey = Guard.Text(key, 1, 48, "question.key", "کلید سؤال معتبر نیست.");
            TemplateVersion = Guard.Text(version, 1, 32, "question.version", "نسخه سؤال معتبر نیست.");
        }

        private void CopyFrom(Question source)
        {
            SourceQuestionId = source.Id;
            Text = source.Text;
            CategoryName = source.Category?.Name ?? string.Empty;
            CategoryIcon = source.Category?.Icon ?? "❓";
            foreach (var o in source.Options.OrderBy(o => o.Order))
                _options.Add(new RoomQuestionOption(Id, o.Order, o.Text));
        }

        public bool HasOption(Guid optionId) => _options.Any(o => o.Id == optionId);

        public bool IsCorrect(Guid? optionId) => optionId.HasValue && optionId == OwnerAnswerOptionId;

        internal void SetOwnerAnswer(Guid optionId)
        {
            Guard.Against(!HasOption(optionId), "room.answer", "گزینه‌ی انتخاب‌شده معتبر نیست.");
            OwnerAnswerOptionId = optionId;
        }

        internal void Generate(string ownerPrompt, string friendPrompt, string categoryName,
            string categoryIcon, IReadOnlyList<Guid> optionOrder, string version)
        {
            Guard.Against(!IsAnsweredByOwner, "question.reference", "پاسخ مرجع لازم است.");
            Guard.Against(optionOrder.Count != _options.Count || optionOrder.Distinct().Count() != _options.Count ||
                optionOrder.Any(id => !HasOption(id)), "question.shuffle", "ترتیب گزینه‌ها معتبر نیست.");
            OwnerPrompt = Guard.Text(ownerPrompt, 5, Question.TextMax, "question.owner", "سؤال پایه معتبر نیست.");
            Text = Guard.Text(friendPrompt, 5, Question.TextMax, "question.friend", "سؤال دوست معتبر نیست.");
            Guard.Against(Text == OwnerPrompt, "question.direct", "سؤال دوست باید متفاوت باشد.");
            CategoryName = categoryName;
            CategoryIcon = categoryIcon;
            GeneratorVersion = version;
            for (var i = 0; i < optionOrder.Count; i++)
                _options.First(o => o.Id == optionOrder[i]).SetOrder(i + 1);
        }
    }

    public class RoomQuestionOption
    {
        public Guid Id { get; private set; }
        public Guid RoomQuestionId { get; private set; }
        public int Order { get; private set; }
        public string Text { get; private set; } = default!;

        private RoomQuestionOption() { }

        internal RoomQuestionOption(Guid roomQuestionId, int order, string text)
        {
            Id = Guid.NewGuid();
            RoomQuestionId = roomQuestionId;
            Order = order;
            Text = text;
        }
        internal void SetOrder(int order) => Order = order;
    }

    public class RoomStatusChange
    {
        public long Id { get; private set; }
        public Guid RoomId { get; private set; }
        public RoomStatus From { get; private set; }
        public RoomStatus To { get; private set; }
        public DateTime At { get; private set; }
        public string? Reason { get; private set; }

        private RoomStatusChange() { }

        internal RoomStatusChange(Guid roomId, RoomStatus from, RoomStatus to, DateTime at, string? reason)
        {
            RoomId = roomId; From = from; To = to; At = at; Reason = reason;
        }
    }
}
