using HotelSaaS.Api.Controllers;
using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Ops;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Application.Services;
using HotelSaaS.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Api.Controllers;

/// <summary>Operational dashboards (PRD sections 32-33).</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(IDashboardService dashboard, ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet("tenant")]
    public async Task<IActionResult> Tenant() =>
        Wrap(await dashboard.GetTenantDashboardAsync());

    [HttpGet("platform")]
    [Authorize(Roles = RoleNames.SuperAdmin)]
    public async Task<IActionResult> Platform() =>
        Wrap(await dashboard.GetPlatformDashboardAsync());
}

/// <summary>Reports (PRD section 34).</summary>
[ApiController]
[Route("api/reports")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.TenantAdmin + "," + RoleNames.Manager + "," +
                   RoleNames.Finance)]
public class ReportsController(IReportService reports) : ApiControllerBase
{
    [HttpGet("occupancy")]
    public async Task<IActionResult> Occupancy(
        [FromQuery] DateTime from, [FromQuery] DateTime to) =>
        Wrap(await reports.OccupancyAsync(from, to));

    [HttpGet("revenue")]
    public async Task<IActionResult> Revenue(
        [FromQuery] DateTime from, [FromQuery] DateTime to) =>
        Wrap(await reports.RevenueAsync(from, to));

    [HttpGet("revenue-by-room-type")]
    public async Task<IActionResult> RevenueByRoomType(
        [FromQuery] DateTime from, [FromQuery] DateTime to) =>
        Wrap(await reports.RevenueByRoomTypeAsync(from, to));

    [HttpGet("bookings-by-source")]
    public async Task<IActionResult> BookingsBySource(
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null) =>
        Wrap(await reports.BookingsBySourceAsync(from, to));

    [HttpGet("cancellations")]
    public async Task<IActionResult> Cancellations([FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var (cancellations, noShows) = await reports.CancellationStatsAsync(from, to);
        return Wrap(new { cancellations, noShows });
    }

    [HttpGet("receivables")]
    public async Task<IActionResult> Receivables(
        [FromQuery] bool corporateOnly = false) =>
        Wrap(await reports.ReceivablesAsync(corporateOnly));

    [HttpGet("guests")]
    public async Task<IActionResult> Guests(
        [FromQuery] int top = 10) =>
        Wrap(await reports.GuestStatsAsync(top));

    [HttpGet("payroll")]
    public async Task<IActionResult> Payroll() =>
        Wrap(await reports.PayrollSummaryAsync());
}

/// <summary>Audit trail - restricted visibility per PRD data ownership matrix.</summary>
[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.TenantAdmin + "," + RoleNames.Manager)]
public class AuditLogsController(AuditService audit, ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] long? tenantId = null,
        [FromQuery] string? entityName = null, [FromQuery] long? userId = null,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Wrap(await audit.SearchAsync(TenantScope.Optional(currentUser, tenantId), entityName, userId,
            from, to, page, pageSize));
}

/// <summary>In-app notification inbox (PRD section 35).</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ApiControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _user;

    public NotificationsController(IAppDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> Mine([FromQuery] int take = 30)
    {
        var tenantId = _user.TenantId;
        var notifications = await _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == _user.UserId ||
                        (n.UserId == null && tenantId != null && n.TenantId == tenantId))
            .OrderByDescending(n => n.CreatedAt).Take(take)
            .Select(n => new { n.NotificationId, n.Title, n.Body, Type = (int)n.Type, n.Status, n.CreatedAt, n.ReadAt })
            .ToListAsync();
        return Wrap(notifications);
    }

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x =>
                    x.NotificationId == id && (x.UserId == _user.UserId ||
                                               (x.UserId == null && x.TenantId == _user.TenantId)))
                ?? throw new NotFoundException("Notification not found.");
        if (n.ReadAt == null)
        {
            n.ReadAt = DateTime.UtcNow;
            n.Status = NotificationStatus.Read;
            await _db.SaveChangesAsync();
        }
        return Wrap(new { id, read = true });
    }
}



