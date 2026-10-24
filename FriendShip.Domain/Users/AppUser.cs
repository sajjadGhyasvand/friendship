using FriendShip.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Domain.Users
{
    public enum UserRole { User = 0, Admin = 1 }

    public class AppUser
    {
        public const int DisplayNameMin = 2;
        public const int DisplayNameMax = 30;
        public const int EmailMax = 256;
        public const int PublicIdLength = 10;
        public const int BioMax = 160;

        public Guid Id { get; private set; }
        public string PublicId { get; private set; } = default!;
        public string Email { get; private set; } = default!;
        public string? UserName { get; private set; }
        public string? GameLinkCode { get; private set; }
        public Guid? CurrentGameId { get; private set; }
        public string? RecoveryCodeHash { get; private set; }
        public string DisplayName { get; private set; } = default!;
        public string PasswordHash { get; private set; } = default!;
        public UserRole Role { get; private set; }
        public string AvatarKey { get; private set; } = AvatarCatalog.DefaultKey;
        public string? Bio { get; private set; }
        public bool IsProfilePublic { get; private set; }
        public bool IsBanned { get; private set; }
        public string? BanReason { get; private set; }

        /// <summary>Rotated on password change / ban; cookies with an old stamp are rejected.</summary>
        public string SecurityStamp { get; private set; } = default!;
        public DateTime CreatedAt { get; private set; }
        public DateTime? LastLoginAt { get; private set; }

        public bool IsAdmin => Role == UserRole.Admin;

        private AppUser() { }

        public static AppUser Register(string email, string displayName, string passwordHash, string publicId, DateTime now)
        {
            var normalized = NormalizeEmail(email);
            Guard.Against(normalized.Length is < 5 or > EmailMax || !normalized.Contains('@') || normalized.StartsWith('@'),
                "user.email", "ایمیل معتبر نیست.");
            Guard.Against(string.IsNullOrWhiteSpace(passwordHash), "user.password", "رمز عبور الزامی است.");
            Guard.Against(publicId is null || publicId.Length != PublicIdLength, "user.publicId", "شناسه‌ی عمومی نامعتبر است.");

            return new AppUser
            {
                Id = Guid.NewGuid(),
                PublicId = publicId!,
                Email = normalized,
                DisplayName = ValidName(displayName),
                PasswordHash = passwordHash,
                Role = UserRole.User,
                IsProfilePublic = true,
                SecurityStamp = NewStamp(),
                CreatedAt = now
            };
        }

        public static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        public static string NormalizeUserName(string? name)
        {
            var value = (name ?? "").Trim().ToLowerInvariant();
            Guard.Against(value.Length is < 3 or > 30 || value.Any(c => !char.IsLetterOrDigit(c) && c != '_'),
                "user.username", "نام کاربری باید ۳ تا ۳۰ حرف، عدد یا زیرخط باشد.");
            return value;
        }

        public static AppUser RegisterSimple(string name, string hash, string recoveryHash, DateTime now)
        {
            var normalized = NormalizeUserName(name);
            // Reserved internal address preserves the existing non-null unique column; no email is collected.
            var user = Register($"{Guid.NewGuid():N}@account.invalid", normalized, hash,
                ShortCode.Generate(PublicIdLength), now);
            user.UserName = normalized;
            user.RecoveryCodeHash = recoveryHash;
            user.IsProfilePublic = false;
            user.EnsureGameLink();
            return user;
        }

        public void EnsureGameLink() => GameLinkCode ??= ShortCode.Generate(24);
        public void SetCurrentGame(Guid? gameId) { EnsureGameLink(); CurrentGameId = gameId; }
        public void ReplaceRecoveryCode(string hash) => RecoveryCodeHash = hash;

        public void UpdateProfile(string displayName, string? avatarKey, string? bio, bool isPublic)
        {
            DisplayName = ValidName(displayName);
            Guard.Against(!AvatarCatalog.IsValid(avatarKey), "user.avatar", "آواتار انتخاب‌شده معتبر نیست.");
            AvatarKey = avatarKey!;
            var cleanBio = Guard.Normalize(bio);
            Guard.Against(cleanBio.Length > BioMax, "user.bio", $"بیو حداکثر {BioMax} حرف می‌تواند باشد.");
            Bio = cleanBio.Length == 0 ? null : cleanBio;
            IsProfilePublic = isPublic;
        }

        public void ChangePassword(string newHash)
        {
            Guard.Against(string.IsNullOrWhiteSpace(newHash), "user.password", "رمز عبور الزامی است.");
            PasswordHash = newHash;
            SecurityStamp = NewStamp();
        }

        /// <summary>Silent rehash (e.g. hasher upgrade) — does not log the user out.</summary>
        public void UpgradePasswordHash(string newHash) => PasswordHash = newHash;

        public void Ban(string? reason)
        {
            Guard.Against(IsAdmin, "user.ban.admin", "مدیر را نمی‌توان مسدود کرد.");
            IsBanned = true;
            BanReason = Guard.Normalize(reason) is { Length: > 0 } r ? r[..Math.Min(r.Length, 200)] : null;
            SecurityStamp = NewStamp();
        }

        public void Unban() { IsBanned = false; BanReason = null; }

        public void PromoteToAdmin() => Role = UserRole.Admin;
        public void DemoteToUser() => Role = UserRole.User;
        public void MarkLogin(DateTime now) => LastLoginAt = now;

        private static string ValidName(string? name) =>
            Guard.Text(name, DisplayNameMin, DisplayNameMax, "user.name",
                $"نام نمایشی باید بین {DisplayNameMin} تا {DisplayNameMax} حرف باشد.");

        private static string NewStamp() => Guid.NewGuid().ToString("N");
    }
}
