using FriendShip.Domain.Common;
using FriendShip.Domain.Questions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Domain.Rooms
{
    public class Room
    {
        public const int CodeLength = 24;
        public const int LegacyCodeLength = 6;
        public const int TitleMin = 3;
        public const int TitleMax = 60;
        public static readonly TimeSpan MinLifetime = TimeSpan.FromHours(1);
        public static readonly TimeSpan MaxLifetime = TimeSpan.FromDays(30);
        public static readonly TimeSpan DefaultLifetime = TimeSpan.FromDays(7);

        public Guid Id { get; private set; }
        public string Code { get; private set; } = default!;
        public Guid OwnerUserId { get; private set; }
        public string Title { get; private set; } = default!;
        public RoomSettings Settings { get; private set; } = default!;
        public RoomStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? OpenedAt { get; private set; }
        public DateTime? EndedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }

        /// <summary>Optimistic concurrency token; regenerated on every state change.</summary>
        public Guid Version { get; private set; }
        public bool IsPersonalGame { get; private set; }
        public string? ScoringVersion { get; private set; }

        private readonly List<RoomQuestion> _questions = new();
        public IReadOnlyList<RoomQuestion> Questions => _questions;

        private readonly List<RoomStatusChange> _history = new();
        public IReadOnlyList<RoomStatusChange> History => _history;

        public bool IsEnded => Status is RoomStatus.Closed or RoomStatus.Expired or RoomStatus.Cancelled;

        /// <summary>Whether guests may see correct answers (after they finished).</summary>
        public bool AreAnswersRevealable =>
            Settings.RevealMode == RevealMode.Immediate || Status is RoomStatus.Closed or RoomStatus.Expired;

        private Room() { }

        /// <param name="questions">Bank questions loaded with Category and Options.</param>
        public static Room Create(string code, Guid ownerUserId, string title, RoomSettings settings,
            IReadOnlyList<Question> questions, DateTime now, TimeSpan? lifetime = null)
        {
            Guard.Against(!ShortCode.IsValid(code, CodeLength), "room.code", "کد اتاق معتبر نیست.");
            Guard.Against(ownerUserId == Guid.Empty, "room.owner", "سازنده‌ی اتاق مشخص نیست.");
            var life = lifetime ?? DefaultLifetime;
            Guard.Against(life < MinLifetime || life > MaxLifetime, "room.lifetime", "مدت اعتبار اتاق معتبر نیست.");
            Guard.Against(questions.Count != settings.QuestionCount, "room.count",
                $"باید دقیقاً {settings.QuestionCount} سؤال انتخاب شود.");
            Guard.Against(questions.Select(q => q.Id).Distinct().Count() != questions.Count,
                "room.duplicate", "یک سؤال نمی‌تواند دو بار در اتاق باشد.");
            Guard.Against(questions.Any(q => !q.IsActive), "room.inactive", "یکی از سؤال‌ها دیگر فعال نیست.");

            var room = new Room
            {
                Id = Guid.NewGuid(),
                Code = code,
                OwnerUserId = ownerUserId,
                Title = Guard.Text(title, TitleMin, TitleMax, "room.title", $"عنوان باید بین {TitleMin} تا {TitleMax} حرف باشد."),
                Settings = settings,
                Status = RoomStatus.Draft,
                CreatedAt = now,
                ExpiresAt = now + life,
                Version = Guid.NewGuid()
            };
            for (var i = 0; i < questions.Count; i++)
                room._questions.Add(new RoomQuestion(room.Id, i + 1, questions[i]));
            return room;
        }

        public RoomQuestion GetQuestion(Guid roomQuestionId) =>
            _questions.FirstOrDefault(q => q.Id == roomQuestionId)
            ?? throw new DomainException("room.question", "سؤال در این اتاق پیدا نشد.");

        public RoomQuestion? QuestionAt(int order) => _questions.FirstOrDefault(q => q.Order == order);

        public void MakePersonal()
        {
            EnsureDraft();
            IsPersonalGame = true;
            ScoringVersion = "comparable-v2";
            ExpiresAt = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
            Touch();
        }

        public void GenerateQuestion(Guid questionId, string ownerPrompt, string friendPrompt,
            string categoryName, string categoryIcon, IReadOnlyList<Guid> optionOrder, string version)
        {
            EnsureDraft();
            GetQuestion(questionId).Generate(ownerPrompt, friendPrompt, categoryName, categoryIcon, optionOrder, version);
            Touch();
        }

        public void SetQuestionTemplate(Guid questionId, string key, string version)
        {
            EnsureDraft();
            GetQuestion(questionId).SetTemplate(key, version);
            Touch();
        }

        public void Pause(DateTime now)
        {
            Guard.Against(Status != RoomStatus.Open, "game.pause", "فقط بازی فعال را می‌توان متوقف کرد.");
            ChangeStatus(RoomStatus.Paused, now, "توقف موقت");
        }

        public void Resume(DateTime now)
        {
            Guard.Against(Status != RoomStatus.Paused, "game.resume", "این بازی متوقف نیست.");
            ChangeStatus(RoomStatus.Open, now, "فعال‌سازی دوباره");
        }

        public void Archive(DateTime now)
        {
            if (Status is RoomStatus.Open or RoomStatus.Paused or RoomStatus.Draft)
            {
                ChangeStatus(RoomStatus.Closed, now, "آرشیو دور قبلی؛ پاسخ‌ها حفظ شدند");
                EndedAt = now;
            }
        }

        // ---------- Draft phase ----------

        public void SetOwnerAnswer(Guid roomQuestionId, Guid optionId)
        {
            EnsureDraft();
            GetQuestion(roomQuestionId).SetOwnerAnswer(optionId);
            Touch();
        }

        public void ReplaceQuestion(Guid roomQuestionId, Question replacement)
        {
            EnsureDraft();
            Guard.Against(!replacement.IsActive, "room.inactive", "این سؤال فعال نیست.");
            var target = GetQuestion(roomQuestionId);
            Guard.Against(_questions.Any(q => q.Id != roomQuestionId && q.SourceQuestionId == replacement.Id),
                "room.duplicate", "این سؤال قبلاً در اتاق هست.");
            target.ReplaceSource(replacement);
            Touch();
        }

        public void Rename(string title)
        {
            Guard.Against(IsEnded, "room.ended", "اتاق تموم شده و قابل ویرایش نیست.");
            Title = Guard.Text(title, TitleMin, TitleMax, "room.title", $"عنوان باید بین {TitleMin} تا {TitleMax} حرف باشد.");
            Touch();
        }

        public void SetLeaderboardVisibility(bool visible)
        {
            Settings.SetLeaderboard(visible);
            Touch();
        }

        // ---------- State machine ----------

        public void Publish(DateTime now)
        {
            ApplyTime(now);
            EnsureDraft();
            Guard.Against(_questions.Any(q => !q.IsAnsweredByOwner), "room.unanswered", "اول باید به همه‌ی سؤال‌ها جواب بدی.");
            ChangeStatus(RoomStatus.Open, now, null);
            OpenedAt = now;
        }

        public void Close(DateTime now)
        {
            ApplyTime(now);
            Guard.Against(Status != RoomStatus.Open, "room.notOpen", "فقط اتاق باز را می‌شود بست.");
            ChangeStatus(RoomStatus.Closed, now, "بسته‌شده توسط سازنده");
            EndedAt = now;
        }

        public void Cancel(DateTime now, string? reason = null)
        {
            Guard.Against(IsEnded, "room.ended", "این اتاق قبلاً تموم شده.");
            ChangeStatus(RoomStatus.Cancelled, now, reason ?? "لغوشده");
            EndedAt = now;
        }

        /// <summary>Lazy expiration. Returns true if the status changed.</summary>
        public bool ApplyTime(DateTime now)
        {
            if (IsEnded || now < ExpiresAt) return false;
            ChangeStatus(RoomStatus.Expired, now, "پایان زمان");
            EndedAt = ExpiresAt;
            return true;
        }

        /// <summary>Validates that a new friend can join. Call <see cref="ApplyTime"/> first.</summary>
        public void EnsureCanJoin(int currentParticipants, Guid? userId, DateTime now)
        {
            Guard.Against(Status == RoomStatus.Paused, "game.paused", "بازی موقتاً غیرفعال است؛ بعداً سر بزن.");
            Guard.Against(Status == RoomStatus.Draft, "room.draft", "این کوییز هنوز آماده نشده.");
            Guard.Against(Status == RoomStatus.Cancelled, "room.cancelled", "این کوییز لغو شده.");
            Guard.Against(Status is RoomStatus.Closed or RoomStatus.Expired || now >= ExpiresAt,
                "room.ended", "مهلت این کوییز تموم شده.");
            Guard.Against(userId.HasValue && userId.Value == OwnerUserId, "room.self",
                "نمی‌تونی کوییز خودت رو جواب بدی! لینک رو برای دوستات بفرست.");
            Guard.Against(currentParticipants >= Settings.MaxParticipants, "room.full", "ظرفیت این کوییز پر شده.");
        }

        private void EnsureDraft() =>
            Guard.Against(Status != RoomStatus.Draft, "room.notDraft", "بعد از انتشار، سؤال‌ها و جواب‌ها قابل تغییر نیستند.");

        private void ChangeStatus(RoomStatus to, DateTime now, string? reason)
        {
            _history.Add(new RoomStatusChange(Id, Status, to, now, reason));
            Status = to;
            Touch();
        }

        private void Touch() => Version = Guid.NewGuid();
    }
}
