using FriendShip.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Domain.Rooms
{
    public class RoomSettings
    {
        public const int MinQuestions = 5;
        public const int MaxQuestions = 20;
        public const int MinParticipants = 2;
        public const int MaxParticipantsLimit = 1000;
        public const int DefaultParticipants = 20;
        public static readonly IReadOnlyList<int> AllowedTimeLimits = new[] { 10, 15, 20, 30 };

        public GameMode Mode { get; private set; }
        public int QuestionCount { get; private set; }
        public int? TimeLimitSeconds { get; private set; }
        public int MaxParticipants { get; private set; }
        public RevealMode RevealMode { get; private set; }
        public bool ShowLeaderboard { get; private set; }

        private RoomSettings() { }

        public static RoomSettings Create(GameMode mode, int questionCount, int? timeLimitSeconds,
            int maxParticipants = DefaultParticipants, RevealMode reveal = RevealMode.Immediate, bool showLeaderboard = true)
        {
            Guard.Against(!Enum.IsDefined(mode), "settings.mode", "حالت بازی معتبر نیست.");
            Guard.Against(!Enum.IsDefined(reveal), "settings.reveal", "نحوه‌ی نمایش جواب‌ها معتبر نیست.");
            Guard.Against(questionCount is < MinQuestions or > MaxQuestions, "settings.count",
                $"تعداد سؤال‌ها باید بین {MinQuestions} تا {MaxQuestions} باشد.");
            Guard.Against(timeLimitSeconds.HasValue && !AllowedTimeLimits.Contains(timeLimitSeconds.Value),
                "settings.time", "محدودیت زمانی معتبر نیست.");
            Guard.Against(mode == GameMode.Speed && !timeLimitSeconds.HasValue,
                "settings.speed", "در حالت سرعتی باید محدودیت زمانی انتخاب شود.");
            Guard.Against(maxParticipants is < MinParticipants or > MaxParticipantsLimit, "settings.max",
                $"حداکثر شرکت‌کننده باید بین {MinParticipants} تا {MaxParticipantsLimit} باشد.");

            return new RoomSettings
            {
                Mode = mode,
                QuestionCount = questionCount,
                TimeLimitSeconds = timeLimitSeconds,
                MaxParticipants = maxParticipants,
                RevealMode = reveal,
                ShowLeaderboard = showLeaderboard
            };
        }

        internal void SetLeaderboard(bool visible) => ShowLeaderboard = visible;
    }
}
