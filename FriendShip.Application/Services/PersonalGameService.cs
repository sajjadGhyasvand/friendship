using FriendShip.Application.Abstractions;
using FriendShip.Application.Common;
using FriendShip.Application.Questions;
using FriendShip.Domain.Common;
using FriendShip.Domain.Rooms;
using Microsoft.EntityFrameworkCore;

namespace FriendShip.Application.Services;

public sealed record PersonalGameInfo(string LinkCode, string? CurrentRoomCode, string OwnerName);
public sealed record ArchivedGame(string Code, string Title, DateTime CreatedAt);

public sealed class PersonalGameService(IAppDbContextFactory dbf, QuestionFactory factory,
    IRateLimiter limiter, TimeProvider time)
{
    public async Task<IReadOnlyList<ArchivedGame>> GetArchiveAsync(Caller caller)
    {
        if (caller.UserId is not Guid id) return Array.Empty<ArchivedGame>();
        await using var db = dbf.Create();
        var current = await db.Users.Where(u => u.Id == id).Select(u => u.CurrentGameId).FirstOrDefaultAsync();
        return await db.Rooms.AsNoTracking().Where(r => r.OwnerUserId == id && r.Id != current)
            .OrderByDescending(r => r.CreatedAt).Take(50)
            .Select(r => new ArchivedGame(r.Code, r.Title, r.CreatedAt)).ToListAsync();
    }

    public Task<Result> DeleteAsync(Caller caller, string code) => Result.RunAsync(async () =>
    {
        if (caller.UserId is not Guid id) return Result.Fail("ابتدا وارد شو.");
        await using var db = dbf.Create();
        var room = await db.Rooms.Include(r => r.Questions).ThenInclude(q => q.Options)
            .Include(r => r.History).FirstOrDefaultAsync(r => r.Code == ShortCode.Normalize(code) && r.OwnerUserId == id);
        if (room is null) return Result.Fail("این بازی پیدا نشد.");
        var user = await db.Users.SingleAsync(u => u.Id == id);
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (user.CurrentGameId == room.Id) { user.SetCurrentGame(null); await db.SaveChangesAsync(); }
        var participants = await db.Participants.Include(p => p.Answers).Where(p => p.RoomId == room.Id).ToListAsync();
        db.Participants.RemoveRange(participants);
        db.Reports.RemoveRange(await db.Reports.Where(r => r.RoomId == room.Id).ToListAsync());
        await db.SaveChangesAsync();
        db.Rooms.Remove(room);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Ok();
    });
    public async Task<PersonalGameInfo?> GetMineAsync(Caller caller)
    {
        if (caller.UserId is not Guid id) return null;
        await using var db = dbf.Create();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsBanned);
        if (user is null) return null;
        if (user.GameLinkCode is null) { user.EnsureGameLink(); await db.SaveChangesAsync(); }
        var code = user.CurrentGameId is Guid gameId
            ? await db.Rooms.Where(r => r.Id == gameId).Select(r => r.Code).FirstOrDefaultAsync() : null;
        return new(user.GameLinkCode!, code, user.DisplayName);
    }

    public async Task<string?> ResolveLinkAsync(string linkCode)
    {
        var normalized = ShortCode.Normalize(linkCode);
        if (!ShortCode.IsValid(normalized, 24)) return null;
        await using var db = dbf.Create();
        return await (from user in db.Users.AsNoTracking()
            join room in db.Rooms.AsNoTracking() on user.CurrentGameId equals (Guid?)room.Id
            where user.GameLinkCode == normalized && !user.IsBanned && room.Status != RoomStatus.Cancelled
            select room.Code).FirstOrDefaultAsync();
    }

    public Task<Result<string>> CreateAsync(Caller caller, IReadOnlyDictionary<string, int> answers, bool newRound) =>
        Result<string>.RunAsync(async () =>
        {
            if (caller.UserId is not Guid id) return Result<string>.Fail("اول وارد حساب شو.");
            if (!limiter.TryAcquire($"personal-create:{id}", 6, TimeSpan.FromHours(1)))
                return Result<string>.Fail("چند دور ساختی؛ کمی بعد دوباره امتحان کن.");
            if (answers.Keys.Any(k => QuestionFactory.Catalog.All(t => t.Key != k)))
                return Result<string>.Fail("اطلاعات سؤال معتبر نیست.");
            var selected = QuestionFactory.Catalog.Where(t => answers.ContainsKey(t.Key)).ToList();
            if (selected.Count < RoomSettings.MinQuestions || selected.Select(t => t.Category).Distinct().Count() < 3)
                return Result<string>.Fail("حداقل ۵ سؤال از ۳ دسته را جواب بده؛ بقیه اختیاری‌اند.");

            await using var db = dbf.Create();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && !u.IsBanned);
            if (user is null) return Result<string>.Fail("حساب در دسترس نیست.");
            Room? previous = null;
            if (user.CurrentGameId is Guid gameId)
            {
                previous = await db.Rooms.FirstOrDefaultAsync(r => r.Id == gameId);
                if (!newRound && previous is not null) return Result<string>.Ok(previous.Code);
            }
            var prompts = selected.Select(t => t.Prompt).ToList();
            var sources = await db.Questions.Include(q => q.Category).Include(q => q.Options)
                .Where(q => prompts.Contains(q.Text) && q.IsActive && q.Category.IsActive).ToListAsync();
            if (prompts.Any(p => sources.Count(q => q.Text == p) != 1))
                return Result<string>.Fail("بانک سؤال آماده نیست؛ مدیر باید آن را راه‌اندازی کند.");
            var now = time.GetUtcNow().UtcDateTime;
            var room = Room.Create(ShortCode.Generate(Room.CodeLength), id, "دوستام چقدر منو می‌شناسن؟",
                RoomSettings.Create(GameMode.Classic, selected.Count, null, 1000, RevealMode.AfterClose, false),
                prompts.Select(p => sources.Single(q => q.Text == p)).ToList(), now);
            room.MakePersonal();
            factory.Generate(room, answers);
            room.Publish(now);
            previous?.Archive(now);
            db.Rooms.Add(room);
            user.SetCurrentGame(room.Id);
            // SaveChanges is atomic, and CurrentGameId prevents simultaneous replacement of a round.
            await db.SaveChangesAsync();
            return Result<string>.Ok(room.Code);
        });

    public async Task<IReadOnlyDictionary<string, int>> GetReferenceAnswersAsync(Caller caller)
    {
        if (caller.UserId is not Guid id) return new Dictionary<string, int>();
        await using var db = dbf.Create();
        var gameId = await db.Users.Where(u => u.Id == id).Select(u => u.CurrentGameId).FirstOrDefaultAsync();
        var room = await db.Rooms.Include(r => r.Questions).ThenInclude(q => q.Options)
            .FirstOrDefaultAsync(r => r.Id == gameId && r.OwnerUserId == id);
        if (room is null) return new Dictionary<string, int>();
        var answers = new Dictionary<string, int>();
        foreach (var q in room.Questions)
        {
            var template = QuestionFactory.Catalog.FirstOrDefault(t => t.Prompt == q.OwnerPrompt);
            var option = q.Options.FirstOrDefault(o => o.Id == q.OwnerAnswerOptionId);
            if (template is not null && option is not null)
                answers[template.Key] = Array.IndexOf(template.Options, option.Text);
        }
        return answers;
    }
}
