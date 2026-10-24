using FriendShip.Application.Abstractions;
using FriendShip.Application.Common;
using FriendShip.Application.Dtos;
using FriendShip.Domain.Common;
using FriendShip.Domain.Users;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Application.Services
{
    public sealed class AuthService(IAppDbContextFactory dbf, IPasswordService passwords, IRateLimiter limiter, TimeProvider time)
    {
        public const int PasswordMin = 8;
        private const string BadLogin = "نام کاربری یا رمز عبور درست نیست.";

        public sealed record SimpleRegistration(AuthUserDto User, string RecoveryCode);

        public Task<Result<SimpleRegistration>> RegisterSimpleAsync(string userName, string password, string ip) =>
            Result<SimpleRegistration>.RunAsync(async () =>
            {
                if (!limiter.TryAcquire($"register:{ip}", 5, TimeSpan.FromHours(1)))
                    return Result<SimpleRegistration>.Fail("تعداد تلاش‌ها زیاد است؛ بعداً امتحان کن.");
                if (string.IsNullOrEmpty(password) || password.Length is < PasswordMin or > 100)
                    return Result<SimpleRegistration>.Fail("رمز عبور باید بین ۸ تا ۱۰۰ نویسه باشد.");
                var name = AppUser.NormalizeUserName(userName);
                await using var db = dbf.Create();
                if (await db.Users.AnyAsync(u => u.UserName == name))
                    return Result<SimpleRegistration>.Fail("این نام کاربری قبلاً انتخاب شده.");
                var recovery = ShortCode.Generate(24);
                var user = AppUser.RegisterSimple(name, passwords.Hash(password), passwords.Hash(recovery), time.GetUtcNow().UtcDateTime);
                db.Users.Add(user);
                try { await db.SaveChangesAsync(); }
                catch (DbUpdateException)
                {
                    await using var verification = dbf.Create();
                    if (await verification.Users.AnyAsync(u => u.UserName == name))
                        return Result<SimpleRegistration>.Fail("این نام کاربری قبلاً انتخاب شده.");
                    throw;
                }
                return Result<SimpleRegistration>.Ok(new(ToDto(user), recovery));
            });

        public Task<Result<string>> RecoverAsync(string userName, string recoveryCode, string newPassword, string ip) =>
            Result<string>.RunAsync(async () =>
            {
                var name = AppUser.NormalizeUserName(userName);
                if (!limiter.TryAcquire($"recover-ip:{ip}", 5, TimeSpan.FromHours(1)) ||
                    !limiter.TryAcquire($"recover-user:{name}", 5, TimeSpan.FromHours(1)))
                    return Result<string>.Fail("تلاش‌های بازیابی زیاد است؛ بعداً امتحان کن.");
                if (string.IsNullOrEmpty(newPassword) || newPassword.Length is < PasswordMin or > 100)
                    return Result<string>.Fail("رمز جدید باید بین ۸ تا ۱۰۰ نویسه باشد.");
                await using var db = dbf.Create();
                var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == name && !u.IsBanned);
                if (user?.RecoveryCodeHash is null || !passwords.Verify(user.RecoveryCodeHash, ShortCode.Normalize(recoveryCode)))
                    return Result<string>.Fail("اطلاعات بازیابی درست نیست.");
                var nextCode = ShortCode.Generate(24);
                user.ChangePassword(passwords.Hash(newPassword));
                user.ReplaceRecoveryCode(passwords.Hash(nextCode));
                await db.SaveChangesAsync();
                return Result<string>.Ok(nextCode);
            });

        public Task<Result<AuthUserDto>> RegisterAsync(string email, string displayName, string password, string ip) =>
            Result<AuthUserDto>.RunAsync(async () =>
            {
                if (!limiter.TryAcquire($"register:{ip}", 5, TimeSpan.FromHours(1)))
                    return Result<AuthUserDto>.Fail("تعداد تلاش‌ها زیاد بود. کمی بعد دوباره امتحان کن.");
                if (string.IsNullOrEmpty(password) || password.Length < PasswordMin || password.Length > 100)
                    return Result<AuthUserDto>.Fail($"رمز عبور باید حداقل {PasswordMin} کاراکتر باشد.");

                await using var db = dbf.Create();
                var normalized = AppUser.NormalizeEmail(email);
                if (await db.Users.AnyAsync(u => u.Email == normalized))
                    return Result<AuthUserDto>.Fail("با این ایمیل قبلاً ثبت‌نام شده.");

                var user = AppUser.Register(email, displayName, passwords.Hash(password),
                    ShortCode.Generate(AppUser.PublicIdLength), time.GetUtcNow().UtcDateTime);
                db.Users.Add(user);
                await db.SaveChangesAsync();
                return Result<AuthUserDto>.Ok(ToDto(user));
            });

        public async Task<Result<AuthUserDto>> LoginAsync(string email, string password, string ip)
        {
            if (email is null || email.Length > 256 || password is null || password.Length > 100)
                return Result<AuthUserDto>.Fail(BadLogin);
            if (!limiter.TryAcquire($"login:{ip}", 10, TimeSpan.FromMinutes(10)))
                return Result<AuthUserDto>.Fail("تعداد تلاش‌ها زیاد بود. چند دقیقه بعد دوباره امتحان کن.");

            await using var db = dbf.Create();
            var normalized = AppUser.NormalizeEmail(email);
            if (!limiter.TryAcquire($"login-user:{normalized}", 10, TimeSpan.FromMinutes(10)))
                return Result<AuthUserDto>.Fail("تعداد تلاش‌ها زیاد بود. چند دقیقه بعد دوباره امتحان کن.");
            if (normalized.Length > 256) return Result<AuthUserDto>.Fail(BadLogin);
            var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == normalized ||
                (u.UserName == null && u.Email == normalized));
            if (user is null || !passwords.Verify(user.PasswordHash, password ?? string.Empty))
                return Result<AuthUserDto>.Fail(BadLogin);
            if (user.IsBanned)
                return Result<AuthUserDto>.Fail("حساب شما مسدود شده است.");

            user.MarkLogin(time.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync();
            return Result<AuthUserDto>.Ok(ToDto(user));
        }

        /// <summary>Called on every cookie validation: false if banned, deleted or stamp rotated.</summary>
        public async Task<bool> IsSessionValidAsync(Guid userId, string stamp)
        {
            await using var db = dbf.Create();
            return await db.Users.AsNoTracking()
                .AnyAsync(u => u.Id == userId && !u.IsBanned && u.SecurityStamp == stamp);
        }

        public async Task<AuthUserDto?> GetAsync(Guid userId)
        {
            await using var db = dbf.Create();
            var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
            return u is null ? null : ToDto(u);
        }

        private static AuthUserDto ToDto(AppUser u) => new(u.Id, u.DisplayName, u.Email, u.Role, u.SecurityStamp);
    }
}
