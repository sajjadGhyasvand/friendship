using FriendShip.Application.Abstractions;
using FriendShip.Application.Common;
using FriendShip.Application.Dtos;
using FriendShip.Domain.Common;
using FriendShip.Domain.Participants;
using FriendShip.Domain.Rooms;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FriendShip.Application.Services
{
    public sealed class PlayService(IAppDbContextFactory dbf, IRateLimiter limiter, TimeProvider time)
    {
        private DateTime Now => time.GetUtcNow().UtcDateTime;

        /// <summary>Joins (or resumes) a room. One participant per visitor per room.</summary>
        public Task<Result> JoinAsync(Caller caller, string code, string displayName, string? avatarKey) =>
            Result.RunAsync(async () =>
            {
                if (!limiter.TryAcquire($"join:{caller.Ip}", 20, TimeSpan.FromMinutes(10)))
                    return Result.Fail("تعداد تلاش‌ها زیاد بود. کمی صبر کن.");

                await using var db = dbf.Create();
                var room = await LoadRoomAsync(db, code);
                if (room is null) return Result.Fail("این کوییز پیدا نشد.");

                var existing = await db.Participants.AnyAsync(p => p.RoomId == room.Id && p.VisitorId == caller.VisitorId);
                if (existing) return Result.Ok();

                if (room.ApplyTime(Now)) await db.SaveChangesAsync();
                var count = await db.Participants.CountAsync(p => p.RoomId == room.Id);
                var p = Participant.Join(room, count, caller.VisitorId, caller.UserId, displayName, avatarKey,
                    ShortCode.Generate(Participant.ShareCodeLength), Now);
                db.Participants.Add(p);
                try
                {
                    await db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Check persisted state using a fresh context.
                    // Do not treat every database error as a successful join.
                    await using var verificationDb = dbf.Create();

                    var joined = await verificationDb.Participants
                        .AsNoTracking()
                        .AnyAsync(p =>
                            p.RoomId == room.Id &&
                            p.VisitorId == caller.VisitorId);

                    if (!joined)
                        throw;
                }
                return Result.Ok();
            });

        public Task<Result<PlayStateDto>> GetStateAsync(Caller caller, string code) =>
            Result<PlayStateDto>.RunAsync(async () =>
            {
                await using var db = dbf.Create();
                var room = await LoadRoomAsync(db, code);
                if (room is null) return Result<PlayStateDto>.Fail("این کوییز پیدا نشد.");
                var p = await LoadParticipantAsync(db, room.Id, caller.VisitorId);
                if (p is null) return Result<PlayStateDto>.Fail("اول باید وارد کوییز بشی.");

                if (p.IsCompleted) return Result<PlayStateDto>.Ok(new(true, null, p.ShareCode));
                if (room.ApplyTime(Now)) await db.SaveChangesAsync();
                if (room.Status == RoomStatus.Paused)
                    return Result<PlayStateDto>.Fail("بازی موقتاً متوقف شده؛ انتخاب‌های ثبت‌شده محفوظ‌اند. بعداً ادامه بده.");

                var served = p.ServeNext(room, Now);
                try { await db.SaveChangesAsync(); }
                catch (DbUpdateException) when (served is not null)
                {
                    // Two refreshes can serve the same next question concurrently.
                    // The unique constraint picks one; resume its persisted state.
                    await using var verification = dbf.Create();
                    var persisted = await verification.Participants.AsNoTracking().AnyAsync(x =>
                        x.RoomId == room.Id && x.VisitorId == caller.VisitorId &&
                        x.Answers.Any(a => a.RoomQuestionId == served.RoomQuestionId));
                    if (!persisted) throw;
                    return await GetStateAsync(caller, code);
                }
                if (served is null) return Result<PlayStateDto>.Ok(new(true, null, p.ShareCode));

                var rq = room.GetQuestion(served.RoomQuestionId);
                int? left = room.Settings.TimeLimitSeconds is int limit
                    ? Math.Max(0, (int)Math.Ceiling(limit - (Now - served.ServedAt).TotalSeconds))
                    : null;
                var dto = new PlayQuestionDto(rq.Id, rq.Order, room.Questions.Count, rq.Text, rq.CategoryName,
                    rq.CategoryIcon, rq.Options.OrderBy(o => o.Order).Select(o => new OptionDto(o.Id, o.Text)).ToList(), left,
                    room.ScoringVersion == "comparable-v2");
                return Result<PlayStateDto>.Ok(new(false, dto, p.ShareCode));
            });

        /// <summary>Records an answer. Correctness is never returned here, only whether the quiz is finished.</summary>
        public Task<Result<bool>> SubmitAsync(Caller caller, string code, Guid roomQuestionId, Guid? optionId) =>
            Result<bool>.RunAsync(async () =>
            {
                if (!limiter.TryAcquire($"answer:{caller.VisitorId}", 60, TimeSpan.FromMinutes(1)))
                    return Result<bool>.Fail("خیلی سریع جواب می‌دی! کمی صبر کن.");

                await using var db = dbf.Create();
                var room = await LoadRoomAsync(db, code);
                if (room is null) return Result<bool>.Fail("این کوییز پیدا نشد.");
                var p = await LoadParticipantAsync(db, room.Id, caller.VisitorId);
                if (p is null) return Result<bool>.Fail("اول باید وارد کوییز بشی.");

                if (room.ApplyTime(Now)) await db.SaveChangesAsync();
                p.SubmitAnswer(room, roomQuestionId, optionId, Now);
                await db.SaveChangesAsync();
                return Result<bool>.Ok(p.IsCompleted);
            });

        private static Task<Room?> LoadRoomAsync(IAppDbContext db, string code) =>
            db.Rooms.Include(r => r.Questions).ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(r => r.Code == ShortCode.Normalize(code));

        private static Task<Participant?> LoadParticipantAsync(IAppDbContext db, Guid roomId, Guid visitorId) =>
            db.Participants.Include(p => p.Answers)
                .FirstOrDefaultAsync(p => p.RoomId == roomId && p.VisitorId == visitorId);
    }
}
