using FriendShip.Domain.Common;
using FriendShip.Domain.Rooms;
using FriendShip.Domain.Scoring;
using FriendShip.Domain.Users;

namespace FriendShip.Domain.Participants;

public class Participant
{
    public const int NameMin = 2;
    public const int NameMax = 30;
    public const int ShareCodeLength = 10;

    /// <summary>Network latency tolerance added to the time limit.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(2);

    public Guid Id { get; private set; }
    public Guid RoomId { get; private set; }
    public Guid VisitorId { get; private set; }
    public Guid? UserId { get; private set; }
    public string DisplayName { get; private set; } = default!;
    public string AvatarKey { get; private set; } = default!;
    public string ShareCode { get; private set; } = default!;
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public int TotalQuestions { get; private set; }
    public int CorrectCount { get; private set; }
    public int Score { get; private set; }
    public int KnowledgePercent { get; private set; }

    private readonly List<ParticipantAnswer> _answers = new();
    public IReadOnlyList<ParticipantAnswer> Answers => _answers;

    public bool IsCompleted => CompletedAt.HasValue;
    public int AnsweredCount => _answers.Count(a => a.IsFinal);

    private Participant() { }

    public static Participant Join(Room room, int currentParticipants, Guid visitorId, Guid? userId,
        string displayName, string? avatarKey, string shareCode, DateTime now)
    {
        Guard.Against(visitorId == Guid.Empty, "participant.visitor", "شناسه‌ی بازدیدکننده نامعتبر است.");
        Guard.Against(!ShortCode.IsValid(shareCode, ShareCodeLength), "participant.share", "کد اشتراک نامعتبر است.");
        room.EnsureCanJoin(currentParticipants, userId, now);

        return new Participant
        {
            Id = Guid.NewGuid(),
            RoomId = room.Id,
            VisitorId = visitorId,
            UserId = userId,
            DisplayName = Guard.Text(displayName, NameMin, NameMax, "participant.name",
                $"اسمت باید بین {NameMin} تا {NameMax} حرف باشد."),
            AvatarKey = AvatarCatalog.OrDefault(avatarKey),
            ShareCode = shareCode,
            StartedAt = now,
            TotalQuestions = room.Questions.Count
        };
    }

    /// <summary>
    /// Returns the current question (locking its ServedAt on first display). Questions whose time
    /// ran out while the guest was away are recorded as timed out. Returns null when finished.
    /// </summary>
    public ParticipantAnswer? ServeNext(Room room, DateTime now)
    {
        EnsureRoom(room);
        while (!IsCompleted)
        {
            var pending = _answers.FirstOrDefault(a => !a.IsFinal);
            if (pending is null)
            {
                var rq = room.QuestionAt(AnsweredCount + 1)
                         ?? throw new DomainException("participant.question", "سؤال بعدی پیدا نشد.");
                Guard.Against(room.Status != RoomStatus.Open, "room.ended", "مهلت این کوییز تموم شده.");
                pending = new ParticipantAnswer(Id, rq.Id, rq.Order, now);
                _answers.Add(pending);
                return pending;
            }

            if (!IsOverdue(room, pending, now)) return pending;

            pending.Finalize(null, isCorrect: false, timedOut: true, points: 0, now);
            CompleteIfDone(room, now);
        }
        return null;
    }

    /// <summary>Submits an answer. <paramref name="optionId"/> null means the client timer ran out.</summary>
    public ParticipantAnswer SubmitAnswer(Room room, Guid roomQuestionId, Guid? optionId, DateTime now)
    {
        EnsureRoom(room);
        var submitted = _answers.FirstOrDefault(a => a.RoomQuestionId == roomQuestionId && a.IsFinal);
        if (submitted is not null)
        {
            Guard.Against(submitted.SelectedOptionId != optionId && !(submitted.TimedOut && optionId is null),
                "participant.duplicate", "این پاسخ قبلاً ثبت شده و قابل تغییر نیست.");
            return submitted;
        }
        Guard.Against(IsCompleted, "participant.completed", "این کوییز رو قبلاً تموم کردی.");
        Guard.Against(room.Status != RoomStatus.Open, "room.ended", "مهلت این کوییز تموم شده.");

        var pending = _answers.FirstOrDefault(a => !a.IsFinal);
        Guard.Against(pending is null || pending.RoomQuestionId != roomQuestionId,
            "participant.order", "این سؤال الان قابل جواب دادن نیست.");

        var rq = room.GetQuestion(roomQuestionId);
        if (optionId.HasValue)
            Guard.Against(!rq.HasOption(optionId.Value), "participant.option", "گزینه‌ی انتخاب‌شده معتبر نیست.");

        var elapsed = now - pending!.ServedAt;
        var timedOut = optionId is null || IsOverdue(room, pending, now);
        var selected = timedOut ? null : optionId;
        var correct = !timedOut && rq.IsCorrect(selected);
        var points = ScoreCalculator.PointsFor(correct, room.Settings.Mode, elapsed, room.Settings.TimeLimitSeconds);

        pending.Finalize(selected, correct, timedOut, points, now);
        CompleteIfDone(room, now);
        return pending;
    }

    private static bool IsOverdue(Room room, ParticipantAnswer a, DateTime now) =>
        room.Settings.TimeLimitSeconds is int limit && now - a.ServedAt > TimeSpan.FromSeconds(limit) + Grace;

    private void CompleteIfDone(Room room, DateTime now)
    {
        if (AnsweredCount < TotalQuestions) return;
        var final = _answers.Where(a => a.IsFinal).ToList();
        CorrectCount = final.Count(a => a.IsCorrect);
        Score = final.Sum(a => a.Points);
        var denominator = room.ScoringVersion == "comparable-v2"
            ? final.Count(a => a.SelectedOptionId.HasValue && !a.TimedOut) : TotalQuestions;
        KnowledgePercent = ScoreCalculator.Percent(CorrectCount, denominator);
        CompletedAt = now;
    }

    private void EnsureRoom(Room room) =>
        Guard.Against(room.Id != RoomId, "participant.room", "اتاق نامعتبر است.");
}

public class ParticipantAnswer
{
    public Guid Id { get; private set; }
    public Guid ParticipantId { get; private set; }
    public Guid RoomQuestionId { get; private set; }
    public int Order { get; private set; }
    public Guid? SelectedOptionId { get; private set; }
    public bool IsCorrect { get; private set; }
    public bool TimedOut { get; private set; }
    public int Points { get; private set; }
    public DateTime ServedAt { get; private set; }
    public DateTime? AnsweredAt { get; private set; }

    public bool IsFinal => AnsweredAt.HasValue;

    private ParticipantAnswer() { }

    internal ParticipantAnswer(Guid participantId, Guid roomQuestionId, int order, DateTime servedAt)
    {
        Id = Guid.NewGuid();
        ParticipantId = participantId;
        RoomQuestionId = roomQuestionId;
        Order = order;
        ServedAt = servedAt;
    }

    internal void Finalize(Guid? selectedOptionId, bool isCorrect, bool timedOut, int points, DateTime now)
    {
        SelectedOptionId = selectedOptionId;
        IsCorrect = isCorrect;
        TimedOut = timedOut;
        Points = points;
        AnsweredAt = now;
    }
}
