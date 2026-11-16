using HotelSaaS.Domain;

namespace HotelSaaS.Domain.Entities;

public class HousekeepingTask
{
    public long TaskId { get; set; }
    public long TenantId { get; set; }
    public long RoomId { get; set; }
    public Room Room { get; set; } = default!;
    public long? BookingId { get; set; }
    public Booking? Booking { get; set; }
    public HousekeepingTaskType TaskType { get; set; } = HousekeepingTaskType.Cleaning;
    public HousekeepingTaskStatus Status { get; set; } = HousekeepingTaskStatus.Pending;
    public long? AssignedToEmployeeId { get; set; }
    public Employee? AssignedToEmployee { get; set; }
    public DateTime ScheduledFor { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? CompletedByUserId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Notification
{
    public long NotificationId { get; set; }
    public long? TenantId { get; set; }
    public long? UserId { get; set; }
    public User? User { get; set; }
    public string Title { get; set; } = default!;
    public string? Body { get; set; }
    public NotificationType Type { get; set; } = NotificationType.Info;
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public string? EntityType { get; set; }
    public long? EntityId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

/// <summary>Append-only audit trail. The application never updates or deletes rows here.</summary>
public class AuditLog
{
    public long AuditLogId { get; set; }
    public long? TenantId { get; set; }
    public long? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = default!;
    public string EntityName { get; set; } = default!;
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedAt { get; set; }
}
