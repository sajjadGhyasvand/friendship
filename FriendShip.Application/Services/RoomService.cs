using FriendShip.Application.Abstractions;
using FriendShip.Application.Common;
using FriendShip.Application.Dtos;
using FriendShip.Domain.Common;
using FriendShip.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Application.Services
{
    public sealed class RoomService(IAppDbContextFactory dbf, IRateLimiter limiter, TimeProvider time)
    {
        private DateTime Now => time.GetUtcNow().UtcDateTime;

        public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync()
        {
            await using var db = dbf.Create();
            return await db.Categories.AsNoTracking().Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder).Select(c => new CategoryDto(c.Id, c.Name, c.Icon)).ToListAsync();
        }

        public async Task<IReadOnlyList<QuestionDto>> GetQuestionsAsync(int? categoryId)
        {
            await using var db = dbf.Create();
            var q = db.Questions.AsNoTracking().Where(x => x.IsActive && x.Category.IsActive);
            if (categoryId.HasValue) q = q.Where(x => x.CategoryId == categoryId.Value);
            return await q.OrderBy(x => x.Id)
                .Select(x => new QuestionDto(x.Id, x.CategoryId, x.Category.Name, x.Text, x.Options.Count))
                .ToListAsync();
        }

        public async Task<IReadOnlyList<int>> SuggestQuestionIdsAsync(int count)
        {
            await using var db = dbf.Create();
            var ids = await db.Questions.AsNoTracking().Where(x => x.IsActive && x.Category.IsActive)
                .Select(x => x.Id).ToListAsync();
            return ids.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
        }

        public Task<Result<string>> CreateAsync(Caller caller, CreateRoomRequest req) =>
            Result<string>.RunAsync(async () =>
            {
                if (caller.UserId is not Guid ownerId) return Result<string>.Fail("برای ساخت کوییز باید وارد شوی.");
                if (!limiter.TryAcquire($"room-create:{ownerId}", 10, TimeSpan.FromHours(1)))
                    return Result<string>.Fail("تعداد کوییزهای ساخته‌شده زیاد است. کمی بعد دوباره امتحان کن.");

                var ids = req.QuestionIds.Distinct().ToList();
                var settings = RoomSettings.Create(req.Mode, ids.Count, req.TimeLimitSeconds,
                    req.MaxParticipants, req.Reveal, req.ShowLeaderboard);

                await using var db = dbf.Create();
                var currentGameId = await db.Users.Where(u => u.Id == ownerId).Select(u => u.CurrentGameId).FirstOrDefaultAsync();
                if (currentGameId.HasValue)
                {
                    var currentCode = await db.Rooms.Where(r => r.Id == currentGameId).Select(r => r.Code).FirstOrDefaultAsync();
                    if (currentCode is not null) return Result<string>.Ok(currentCode);
                }
                var loaded = await db.Questions.Include(q => q.Category).Include(q => q.Options)
                 .Where(q =>
    ids.Contains(q.Id) &&
    q.IsActive &&
    q.Category.IsActive)
.ToListAsync();

                if (loaded.Count != ids.Count)
                {
                    return Result<string>.Fail(
                        "بعضی از سؤال‌ها پیدا نشدند یا دیگر فعال نیستند.");
                }
                var ordered = ids.Select(id => loaded.First(q => q.Id == id)).ToList();

                string code;
                var attempts = 0;
                do
                {
                    code = ShortCode.Generate(Room.CodeLength);
                    if (++attempts > 8) return Result<string>.Fail("ساخت کد انجام نشد. دوباره تلاش کن.");
                } while (await db.Rooms.AnyAsync(r => r.Code == code));

                var room = Room.Create(code, ownerId, req.Title, settings, ordered, Now);
                db.Rooms.Add(room);
                await db.SaveChangesAsync();
                return Result<string>.Ok(room.Code);
            });

        /// <summary>Owner-only. Returns null if the room doesn't exist or isn't owned by the caller.</summary>
        public async Task<OwnerRoomDto?> GetOwnerViewAsync(Caller caller, string code)
        {
            if (caller.UserId is not Guid uid) return null;
            await using var db = dbf.Create();
            var room = await LoadAsync(db, code);
            if (room is null || room.OwnerUserId != uid) return null;
            if (room.ApplyTime(Now)) await db.SaveChangesAsync();

            var count = await db.Participants.CountAsync(p => p.RoomId == room.Id);
            var qs = room.Questions.OrderBy(q => q.Order).Select(q => new OwnerQuestionDto(
                q.Id, q.Order, q.OwnerPrompt ?? q.Text, q.CategoryName, q.CategoryIcon,
                q.Options.OrderBy(o => o.Order).Select(o => new OptionDto(o.Id, o.Text)).ToList(),
                q.OwnerAnswerOptionId)).ToList();
            return new OwnerRoomDto(room.Code, room.Title, room.Status, room.ExpiresAt, room.Settings.Mode,
                room.Settings.TimeLimitSeconds, count, room.Settings.MaxParticipants, qs);
        }

        public Task<Result> SetOwnerAnswerAsync(Caller caller, string code, Guid roomQuestionId, Guid optionId) =>
            MutateOwnedAsync(caller, code, (room, _) => room.SetOwnerAnswer(roomQuestionId, optionId));

        public Task<Result> PublishAsync(Caller caller, string code) =>
            MutateOwnedAsync(caller, code, (room, now) => room.Publish(now));

        public Task<Result> CloseAsync(Caller caller, string code) =>
            MutateOwnedAsync(caller, code, (room, now) => room.Close(now));

        public Task<Result> CancelAsync(Caller caller, string code) =>
            MutateOwnedAsync(caller, code, (room, now) => room.Cancel(now));

        public Task<Result> PauseAsync(Caller caller, string code) =>
            MutateOwnedAsync(caller, code, (room, now) => room.Pause(now));
        public Task<Result> ResumeAsync(Caller caller, string code) =>
            MutateOwnedAsync(caller, code, (room, now) => room.Resume(now));

        public async Task<RoomPublicDto?> GetPublicAsync(Caller caller, string code)
        {
            await using var db = dbf.Create();
            var room = await db.Rooms.Include(r => r.Questions).FirstOrDefaultAsync(r => r.Code == ShortCode.Normalize(code));
            if (room is null || room.Status == RoomStatus.Draft && room.OwnerUserId != caller.UserId) return null;
            if (room.ApplyTime(Now)) await db.SaveChangesAsync();

            var owner = await db.Users.AsNoTracking().Where(u => u.Id == room.OwnerUserId)
                .Select(u => new { u.DisplayName, u.AvatarKey }).FirstAsync();
            var count = await db.Participants.CountAsync(p => p.RoomId == room.Id);
            return new RoomPublicDto(room.Code, room.Title, owner.DisplayName, owner.AvatarKey, room.Status,
                room.Questions.Count, count, room.Settings.MaxParticipants, room.Settings.TimeLimitSeconds,
                room.Settings.Mode, room.ExpiresAt, caller.UserId == room.OwnerUserId);
        }

        private Task<Result> MutateOwnedAsync(Caller caller, string code, Action<Room, DateTime> action) =>
            Result.RunAsync(async () =>
            {
                if (caller.UserId is not Guid uid) return Result.Fail("ابتدا وارد شو.");
                await using var db = dbf.Create();
                var room = await LoadAsync(db, code);
                if (room is null || room.OwnerUserId != uid) return Result.Fail("این کوییز پیدا نشد.");
                action(room, Now);
                await db.SaveChangesAsync();
                return Result.Ok();
            });

        private static Task<Room?> LoadAsync(IAppDbContext db, string code) =>
            db.Rooms.Include(r => r.Questions).ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(r => r.Code == ShortCode.Normalize(code));
    }
}
