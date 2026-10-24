using FriendShip.Application.Abstractions;
using FriendShip.Application.Common;
using FriendShip.Application.Dtos;
using FriendShip.Domain.Moderation;
using FriendShip.Domain.Rooms;
using FriendShip.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FriendShip.Application.Services;

public sealed class AdminService(
    IAppDbContextFactory dbf,
    TimeProvider time)
{
    private const string AccessDenied =
        "اجازه‌ی دسترسی به بخش مدیریت را نداری.";

    private const int MaxPageSize = 100;
    private const int MaxPage = 1_000_000;

    private DateTime Now => time.GetUtcNow().UtcDateTime;

    public Task<Result<AdminDashboardDto>> GetDashboardAsync(Caller caller) =>
        ReadAsync(caller, async db =>
        {
            var now = Now;
            var since = now.AddHours(-24);

            var users = await db.Users.CountAsync();
            var rooms = await db.Rooms.CountAsync();

            // Exclude lazily expired rooms even if their stored status
            // has not been updated yet.
            var openRooms = await db.Rooms.CountAsync(r =>
                r.Status == RoomStatus.Open &&
                r.ExpiresAt > now);

            var participants = await db.Participants.CountAsync();

            var pendingReports = await db.Reports.CountAsync(r =>
                r.Status == ReportStatus.Pending);

            var errors = await db.ErrorLogs.CountAsync(e =>
                e.At >= since &&
                e.At <= now &&
                (e.Level == "Error" || e.Level == "Critical"));

            return new AdminDashboardDto(
                users,
                rooms,
                openRooms,
                participants,
                pendingReports,
                errors);
        });

    public Task<Result<PagedResult<AdminUserDto>>> GetUsersAsync(
        Caller caller,
        string? search = null,
        bool? isBanned = null,
        int page = 1,
        int pageSize = 20) =>
        ReadAsync(caller, async db =>
        {
            var paging = NormalizePaging(page, pageSize);

            var query = db.Users.AsNoTracking();

            var term = CleanSearch(search);

            if (term is not null)
            {
                var normalizedEmail = AppUser.NormalizeEmail(term);

                query = query.Where(u =>
                    u.DisplayName.Contains(term) ||
                    u.Email.Contains(normalizedEmail) ||
                    u.PublicId.Contains(term));
            }

            if (isBanned.HasValue)
                query = query.Where(u => u.IsBanned == isBanned.Value);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(u => u.CreatedAt)
                .ThenBy(u => u.Id)
                .Skip(paging.Skip)
                .Take(paging.PageSize)
                .Select(u => new AdminUserDto(
                    u.Id,
                    u.DisplayName,
                    u.Email,
                    u.Role,
                    u.IsBanned,
                    u.BanReason,
                    u.CreatedAt,
                    u.LastLoginAt))
                .ToListAsync();

            return new PagedResult<AdminUserDto>(
                items,
                total,
                paging.Page,
                paging.PageSize);
        });

    public Task<Result> BanUserAsync(
        Caller caller,
        Guid userId,
        string? reason) =>
        MutateAsync(caller, async db =>
        {
            if (caller.UserId == userId)
                return Result.Fail("نمی‌توانی حساب خودت را مسدود کنی.");

            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                return Result.Fail("کاربر پیدا نشد.");

            if (user.IsAdmin)
                return Result.Fail("حساب مدیر را نمی‌توان مسدود کرد.");

            // Domain rotates the security stamp, invalidating old sessions.
            user.Ban(reason);

            return Result.Ok();
        });

    public Task<Result> UnbanUserAsync(
        Caller caller,
        Guid userId) =>
        MutateAsync(caller, async db =>
        {
            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null)
                return Result.Fail("کاربر پیدا نشد.");

            user.Unban();

            return Result.Ok();
        });

    public Task<Result<PagedResult<AdminReportDto>>> GetReportsAsync(
        Caller caller,
        ReportStatus? status = ReportStatus.Pending,
        int page = 1,
        int pageSize = 20) =>
        ReadAsync(caller, async db =>
        {
            var paging = NormalizePaging(page, pageSize);

            var reports = db.Reports.AsNoTracking();

            if (status.HasValue)
                reports = reports.Where(r => r.Status == status.Value);

            var query =
                from report in reports
                join room in db.Rooms.AsNoTracking()
                    on report.RoomId equals room.Id
                select new
                {
                    report.Id,
                    RoomCode = room.Code,
                    RoomTitle = room.Title,
                    report.Reason,
                    report.Status,
                    report.CreatedAt
                };

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .ThenBy(r => r.Id)
                .Skip(paging.Skip)
                .Take(paging.PageSize)
                .Select(r => new AdminReportDto(
                    r.Id,
                    r.RoomCode,
                    r.RoomTitle,
                    r.Reason,
                    r.Status,
                    r.CreatedAt))
                .ToListAsync();

            return new PagedResult<AdminReportDto>(
                items,
                total,
                paging.Page,
                paging.PageSize);
        });

    public Task<Result> ResolveReportAsync(
        Caller caller,
        Guid reportId) =>
        HandleReportAsync(caller, reportId, dismiss: false);

    public Task<Result> DismissReportAsync(
        Caller caller,
        Guid reportId) =>
        HandleReportAsync(caller, reportId, dismiss: true);

    public Task<Result> CancelRoomAsync(
        Caller caller,
        string code,
        string? reason) =>
        MutateAsync(caller, async db =>
        {
            var normalizedCode =
                FriendShip.Domain.Common.ShortCode.Normalize(code);

            var room = await db.Rooms
                .FirstOrDefaultAsync(r => r.Code == normalizedCode);

            if (room is null)
                return Result.Fail("این کوییز پیدا نشد.");

            // Cancellation remains subject to the domain state machine.
            // In particular, an already-ended room cannot be cancelled.
            var cleanReason = reason?.Trim();

            if (string.IsNullOrWhiteSpace(cleanReason))
                cleanReason = "لغوشده توسط مدیر";

            if (cleanReason.Length > 200)
                return Result.Fail("دلیل لغو حداکثر ۲۰۰ حرف می‌تواند باشد.");

            room.Cancel(Now, cleanReason);

            return Result.Ok();
        });

    public Task<Result<PagedResult<AdminErrorDto>>> GetErrorsAsync(
        Caller caller,
        string? level = null,
        int page = 1,
        int pageSize = 20) =>
        ReadAsync(caller, async db =>
        {
            var paging = NormalizePaging(page, pageSize);
            var query = db.ErrorLogs.AsNoTracking();

            var cleanLevel = level?.Trim();

            if (!string.IsNullOrWhiteSpace(cleanLevel))
                query = query.Where(e => e.Level == cleanLevel);

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(e => e.At)
                .ThenByDescending(e => e.Id)
                .Skip(paging.Skip)
                .Take(paging.PageSize)
                .Select(e => new AdminErrorDto(
                    e.Id,
                    e.At,
                    e.Level,
                    e.Category,
                    e.Message,
                    e.Exception))
                .ToListAsync();

            return new PagedResult<AdminErrorDto>(
                items,
                total,
                paging.Page,
                paging.PageSize);
        });

    private Task<Result> HandleReportAsync(
        Caller caller,
        Guid reportId,
        bool dismiss) =>
        MutateAsync(caller, async db =>
        {
            var report = await db.Reports
                .FirstOrDefaultAsync(r => r.Id == reportId);

            if (report is null)
                return Result.Fail("گزارش پیدا نشد.");

            // The wrapper has already verified a real active admin.
            var adminId = caller.UserId!.Value;

            if (dismiss)
                report.Dismiss(adminId, Now);
            else
                report.Resolve(adminId, Now);

            return Result.Ok();
        });

    private Task<Result<T>> ReadAsync<T>(
        Caller caller,
        Func<IAppDbContext, Task<T>> action) =>
        Result<T>.RunAsync(async () =>
        {
            await using var db = dbf.Create();

            if (!await IsActiveAdminAsync(db, caller))
                return Result<T>.Fail(AccessDenied);

            return Result<T>.Ok(await action(db));
        });

    private Task<Result> MutateAsync(
        Caller caller,
        Func<IAppDbContext, Task<Result>> action) =>
        Result.RunAsync(async () =>
        {
            await using var db = dbf.Create();

            if (!await IsActiveAdminAsync(db, caller))
                return Result.Fail(AccessDenied);

            var result = await action(db);

            if (!result.Succeeded)
                return result;

            await db.SaveChangesAsync();

            return Result.Ok();
        });

    private static Task<bool> IsActiveAdminAsync(
        IAppDbContext db,
        Caller caller)
    {
        if (caller.UserId is not Guid userId)
            return Task.FromResult(false);

        return db.Users
            .AsNoTracking()
            .AnyAsync(u =>
                u.Id == userId &&
                u.Role == UserRole.Admin &&
                !u.IsBanned);
    }

    private static (int Page, int PageSize, int Skip) NormalizePaging(
        int page,
        int pageSize)
    {
        page = Math.Clamp(page, 1, MaxPage);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        return (page, pageSize, (page - 1) * pageSize);
    }

    private static string? CleanSearch(string? search)
    {
        var term = search?.Trim();

        if (string.IsNullOrEmpty(term))
            return null;

        return term.Length <= AppUser.EmailMax
            ? term
            : term[..AppUser.EmailMax];
    }
}
