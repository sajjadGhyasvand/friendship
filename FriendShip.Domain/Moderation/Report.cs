using FriendShip.Domain.Common;

namespace FriendShip.Domain.Moderation;

public enum ReportStatus { Pending = 0, Resolved = 1, Dismissed = 2 }

public class Report
{
    public const int ReasonMin = 3;
    public const int ReasonMax = 300;

    public Guid Id { get; private set; }
    public Guid RoomId { get; private set; }
    public Guid? ReporterUserId { get; private set; }
    public Guid ReporterVisitorId { get; private set; }
    public string Reason { get; private set; } = default!;
    public ReportStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? HandledAt { get; private set; }
    public Guid? HandledByUserId { get; private set; }

    private Report() { }

    public static Report Create(Guid roomId, Guid? userId, Guid visitorId, string reason, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        RoomId = roomId,
        ReporterUserId = userId,
        ReporterVisitorId = visitorId,
        Reason = Guard.Text(reason, ReasonMin, ReasonMax, "report.reason",
            $"دلیل گزارش باید بین {ReasonMin} تا {ReasonMax} حرف باشد."),
        Status = ReportStatus.Pending,
        CreatedAt = now
    };

    public void Resolve(Guid adminId, DateTime now) => Handle(ReportStatus.Resolved, adminId, now);
    public void Dismiss(Guid adminId, DateTime now) => Handle(ReportStatus.Dismissed, adminId, now);

    private void Handle(ReportStatus to, Guid adminId, DateTime now)
    {
        Guard.Against(Status != ReportStatus.Pending, "report.handled", "این گزارش قبلاً بررسی شده.");
        Status = to; HandledAt = now; HandledByUserId = adminId;
    }
}
