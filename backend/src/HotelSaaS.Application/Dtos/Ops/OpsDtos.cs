using FluentValidation;
using HotelSaaS.Domain;

namespace HotelSaaS.Application.Dtos.Ops;

public record HousekeepingTaskDto(long TaskId, long RoomId, string RoomNumber, long? BookingId,
    string? BookingNumber, HousekeepingTaskType TaskType, HousekeepingTaskStatus Status,
    long? AssignedToEmployeeId, string? AssignedToName, DateTime ScheduledFor, DateTime? CompletedAt, string? Notes);

public record CreateHousekeepingTaskRequest(long RoomId, HousekeepingTaskType TaskType, DateTime ScheduledFor,
    long? AssignedToEmployeeId = null, string? Notes = null);

public class CreateHousekeepingTaskValidator : AbstractValidator<CreateHousekeepingTaskRequest>
{
    public CreateHousekeepingTaskValidator()
    {
        RuleFor(x => x.RoomId).NotEmpty();
        RuleFor(x => x.TaskType).IsInEnum();
        RuleFor(x => x.ScheduledFor).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public record UpdateHousekeepingTaskStatusRequest(HousekeepingTaskStatus Status);

// ---- Dashboard & Reports ----

public record TenantDashboardDto(
    int TotalRooms, int AvailableRooms, int OccupiedRooms, int ReservedRooms,
    int CleaningRooms, int MaintenanceRooms,
    int TodaysArrivals, int TodaysDepartures, int CurrentGuests,
    decimal TodaysRevenue, decimal OutstandingBalance, int PendingBookings, decimal OccupancyPercent,
    IReadOnlyList<ArrivalDepartureDto> ArrivalsToday, IReadOnlyList<ArrivalDepartureDto> DeparturesToday);

public record ArrivalDepartureDto(long BookingId, string BookingNumber, string GuestName, string RoomNumbers,
    DateTime Time, BookingStatus Status);

public record PlatformDashboardDto(
    int TotalTenants, int ActiveTenants, int InactiveTenants, int SuspendedTenants,
    int TotalUsers, int ActiveUsers, int TotalRooms, int TotalBookings,
    IReadOnlyList<RecentTenantDto> RecentTenants, double DbLatencyMs);

public record RecentTenantDto(long TenantId, string TenantCode, string BusinessName, TenantStatus Status,
    DateTime CreatedAt);

public record OccupancyPointDto(DateOnly Date, int TotalRooms, int OccupiedRooms, decimal OccupancyPercent);
public record RevenuePointDto(DateTime Date, decimal Payments, decimal Invoiced);
public record BookingsBySourceDto(string Source, int Count);
public record ReceivableDto(long InvoiceId, string InvoiceNumber, string BilledTo, bool IsCorporate,
    DateTime InvoiceDate, DateOnly? DueDate, decimal BalanceDue);
public record GuestStatDto(long GuestId, string Name, int StayCount, decimal TotalSpent);
public record PayrollSummaryDto(int Year, int Month, PayrollRunStatus Status, decimal Gross, decimal Net, int Employees);
public record DepartmentPayrollDto(string Department, decimal Gross, decimal Net, int Employees);
public record RevenueByRoomTypeDto(string RoomType, decimal Amount, int ChargesCount);
