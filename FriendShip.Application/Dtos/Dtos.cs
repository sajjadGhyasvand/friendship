using FriendShip.Domain.Rooms;
using FriendShip.Domain.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Application.Dtos
{

    public sealed record AuthUserDto(Guid Id, string DisplayName, string Email, UserRole Role, string SecurityStamp);

    public sealed record CategoryDto(int Id, string Name, string Icon);
    public sealed record QuestionDto(int Id, int CategoryId, string CategoryName, string Text, int OptionCount);

    public sealed record CreateRoomRequest(
        string Title, GameMode Mode, int? TimeLimitSeconds, int MaxParticipants,
        RevealMode Reveal, bool ShowLeaderboard, IReadOnlyList<int> QuestionIds);

    public sealed record OptionDto(Guid Id, string Text);

    /// <summary>Owner-only view. Contains the owner's own answers.</summary>
    public sealed record OwnerQuestionDto(Guid Id, int Order, string Text, string CategoryName, string CategoryIcon,
        IReadOnlyList<OptionDto> Options, Guid? OwnerAnswerOptionId);

    public sealed record OwnerRoomDto(string Code, string Title, RoomStatus Status, DateTime ExpiresAt,
        GameMode Mode, int? TimeLimitSeconds, int ParticipantCount, int MaxParticipants,
        IReadOnlyList<OwnerQuestionDto> Questions)
    {
        public int AnsweredCount => Questions.Count(q => q.OwnerAnswerOptionId.HasValue);
    }

    /// <summary>Public info for the invite page. Never includes answers.</summary>
    public sealed record RoomPublicDto(string Code, string Title, string OwnerName, string OwnerAvatarKey,
        RoomStatus Status, int QuestionCount, int ParticipantCount, int MaxParticipants,
        int? TimeLimitSeconds, GameMode Mode, DateTime ExpiresAt, bool IsOwner);

    public sealed record PlayQuestionDto(Guid RoomQuestionId, int Number, int Total, string Text,
        string CategoryName, string CategoryIcon, IReadOnlyList<OptionDto> Options, int? SecondsLeft, bool CanSkip = false);

    public sealed record PlayStateDto(bool Finished, PlayQuestionDto? Question, string ShareCode);
}
