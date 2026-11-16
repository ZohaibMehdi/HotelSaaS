using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Ops;
using HotelSaaS.Application.Dtos.Rooms;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Domain;
using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Application.Services;

public interface IHousekeepingService
{
    Task<IReadOnlyList<HousekeepingTaskDto>> GetTasksAsync(HousekeepingTaskStatus? status,
        DateTime? date, CancellationToken ct = default);
    Task<HousekeepingTaskDto> CreateTaskAsync(CreateHousekeepingTaskRequest request, CancellationToken ct = default);
    Task UpdateTaskStatusAsync(long taskId, HousekeepingTaskStatus newStatus, CancellationToken ct = default);
    /// <summary>Quick room-status action for housekeeping staff (PRD section 7.7).</summary>
    Task SetRoomHousekeepingAsync(long roomId, UpdateHousekeepingRequest request, CancellationToken ct = default);
}

public class HousekeepingService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAuditService audit,
    RoomService roomService) : IHousekeepingService
{
    public async Task<IReadOnlyList<HousekeepingTaskDto>> GetTasksAsync(HousekeepingTaskStatus? status,
        DateTime? date, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var q = db.HousekeepingTasks.AsNoTracking()
            .Include(t => t.Room).Include(t => t.AssignedToEmployee).Include(t => t.Booking)
            .Where(t => t.TenantId == tenantId && t.IsActive);

        if (status.HasValue) q = q.Where(t => t.Status == status.Value);
        if (date.HasValue)
        {
            var d = date.Value.Date;
            q = q.Where(t => t.ScheduledFor >= d && t.ScheduledFor < d.AddDays(1));
        }

        var tasks = await q.OrderBy(t => t.ScheduledFor).Take(300).ToListAsync(ct);
        return tasks.Select(Map).ToList();
    }

    public async Task<HousekeepingTaskDto> CreateTaskAsync(CreateHousekeepingTaskRequest request,
        CancellationToken ct = default)
    {
        Validate.Input(new CreateHousekeepingTaskValidator(), request);
        var tenantId = currentUser.RequireTenantId();

        var room = await db.Rooms.FirstOrDefaultAsync(r =>
                r.RoomId == request.RoomId && r.TenantId == tenantId, ct)
            ?? throw new BadRequestException("Room not found in this property.");
        if (request.AssignedToEmployeeId.HasValue &&
            !await db.Employees.AnyAsync(e =>
                e.EmployeeId == request.AssignedToEmployeeId.Value && e.TenantId == tenantId, ct))
            throw new BadRequestException("Assigned employee not found in this property.");

        var task = new HousekeepingTask
        {
            TenantId = tenantId, RoomId = room.RoomId, TaskType = request.TaskType,
            Status = HousekeepingTaskStatus.Pending, AssignedToEmployeeId = request.AssignedToEmployeeId,
            ScheduledFor = request.ScheduledFor, Notes = request.Notes,
            CreatedBy = currentUser.UserId, CreatedAt = DateTime.UtcNow
        };
        if (request.TaskType == HousekeepingTaskType.Maintenance && room.Status != RoomStatus.Occupied)
            room.Status = RoomStatus.Maintenance;
        db.HousekeepingTasks.Add(task);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Create", nameof(HousekeepingTask), task.TaskId, null,
            new { Room = room.RoomNumber, Type = request.TaskType.ToString() }, tenantId, ct);
        return Map(await ReloadAsync(task.TaskId, tenantId, ct));
    }

    public async Task UpdateTaskStatusAsync(long taskId, HousekeepingTaskStatus newStatus,
        CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var task = await db.HousekeepingTasks
            .Include(t => t.Room).Include(t => t.AssignedToEmployee).Include(t => t.Booking)
            .FirstOrDefaultAsync(t => t.TaskId == taskId && t.TenantId == tenantId, ct)
            ?? throw new NotFoundException("Housekeeping task not found.");
        if (task.Status is HousekeepingTaskStatus.Completed or HousekeepingTaskStatus.Cancelled)
            throw new BusinessRuleException("Closed tasks cannot be modified.");

        switch (newStatus)
        {
            case HousekeepingTaskStatus.InProgress:
                task.Status = newStatus;
                task.StartedAt = DateTime.UtcNow;
                if (task.Room!.Status == RoomStatus.Cleaning || task.Room.HousekeepingStatus == HousekeepingStatus.Dirty)
                    task.Room.HousekeepingStatus = HousekeepingStatus.Cleaning;
                break;
            case HousekeepingTaskStatus.Completed:
                Complete(task);
                break;
            case HousekeepingTaskStatus.Cancelled:
                task.Status = newStatus;
                break;
            default:
                throw new BusinessRuleException("Invalid task status transition.");
        }
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("UpdateStatus", nameof(HousekeepingTask), taskId, null,
            new { Status = newStatus.ToString() }, tenantId, ct);
    }

    private void Complete(HousekeepingTask task)
    {
        task.Status = HousekeepingTaskStatus.Completed;
        task.CompletedAt = DateTime.UtcNow;
        task.CompletedByUserId = currentUser.UserId;

        // PRD section 31 lifecycle: Dirty -> Cleaning -> Clean -> Available.
        if (task.TaskType == HousekeepingTaskType.Inspection)
        {
            task.Room!.HousekeepingStatus = HousekeepingStatus.Inspected;
        }
        else
        {
            task.Room!.HousekeepingStatus = HousekeepingStatus.Clean;
        }
        if (task.Room.Status is RoomStatus.Cleaning or RoomStatus.Occupied or RoomStatus.Reserved)
            task.Room.Status = RoomStatus.Available;
        task.Room.UpdatedAt = DateTime.UtcNow;
        task.Room.UpdatedBy = currentUser.UserId;
    }

    public async Task SetRoomHousekeepingAsync(long roomId, UpdateHousekeepingRequest request,
        CancellationToken ct = default)
    {
        await roomService.UpdateHousekeepingAsync(roomId, request, ct);
    }

    private async Task<HousekeepingTask> ReloadAsync(long id, long tenantId, CancellationToken ct) =>
        await db.HousekeepingTasks.AsNoTracking()
            .Include(t => t.Room).Include(t => t.AssignedToEmployee).Include(t => t.Booking)
            .FirstAsync(t => t.TaskId == id && t.TenantId == tenantId, ct);

    internal static HousekeepingTaskDto Map(HousekeepingTask t) => new(
        t.TaskId, t.RoomId, t.Room?.RoomNumber ?? string.Empty, t.BookingId, t.Booking?.BookingNumber,
        t.TaskType, t.Status, t.AssignedToEmployeeId,
        t.AssignedToEmployee == null ? null : $"{t.AssignedToEmployee.FirstName} {t.AssignedToEmployee.LastName}".Trim(),
        t.ScheduledFor, t.CompletedAt, t.Notes);
}
