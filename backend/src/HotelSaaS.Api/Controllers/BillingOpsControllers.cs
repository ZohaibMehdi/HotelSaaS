using HotelSaaS.Api.Controllers;
using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Billing;
using HotelSaaS.Application.Dtos.Ops;
using HotelSaaS.Application.Dtos.Rooms;
using HotelSaaS.Application.Services;
using HotelSaaS.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelSaaS.Api.Controllers;

/// <summary>Invoices + payments (PRD sections 24-25).</summary>
[ApiController]
[Route("api/invoices")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.TenantAdmin + "," + RoleNames.Manager + "," +
                   RoleNames.FrontDesk + "," + RoleNames.Finance)]
public class InvoicesController(IInvoiceService invoices) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] InvoiceStatus? status = null, [FromQuery] long? corporateCustomerId = null) =>
        Wrap(await invoices.SearchAsync(page, pageSize, search, status, corporateCustomerId));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id) => Wrap(await invoices.GetByIdAsync(id));

    /// <summary>Print/PDF-ready representation (PRD section 24) - same payload, printable in UI.</summary>
    [HttpGet("{id:long}/print")]
    public async Task<IActionResult> Print(long id) =>
        Wrap(await invoices.GetByIdAsync(id));

    [HttpPost("{id:long}/payments")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Finance + "," + RoleNames.FrontDesk)]
    public async Task<IActionResult> RecordPayment(long id, RecordPaymentRequest request,
        [FromQuery] bool allowOverpayment = false) =>
        Created(await invoices.RecordPaymentAsync(id, request, allowOverpayment), "Payment recorded.");

    [HttpPost("{id:long}/cancel")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager + "," + RoleNames.Finance)]
    public async Task<IActionResult> Cancel(long id, [FromBody] CancelInvoiceRequest request)
    {
        await invoices.CancelAsync(id, request.Reason);
        return Wrap(new { id, cancelled = true });
    }
}

public record CancelInvoiceRequest(string Reason);

[ApiController]
[Route("api/payments")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.TenantAdmin + "," + RoleNames.Manager + "," +
                   RoleNames.FrontDesk + "," + RoleNames.Finance)]
public class PaymentsController(IInvoiceService invoices) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Recent(
        [FromQuery] int take = 100) =>
        Wrap(await invoices.RecentPaymentsAsync(take));
}

/// <summary>Housekeeping task board (PRD section 31).</summary>
[ApiController]
[Route("api/housekeeping")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.TenantAdmin + "," + RoleNames.Manager + "," +
                   RoleNames.Housekeeping + "," + RoleNames.FrontDesk)]
public class HousekeepingController(HousekeepingService housekeeping, IRoomService rooms) : ApiControllerBase
{
    [HttpGet("tasks")]
    public async Task<IActionResult> Tasks(
        [FromQuery] HousekeepingTaskStatus? status = null, [FromQuery] DateTime? date = null) =>
        Wrap(await housekeeping.GetTasksAsync(status, date));

    [HttpPost("tasks")]
    public async Task<IActionResult> Create(CreateHousekeepingTaskRequest request) =>
        Created(await housekeeping.CreateTaskAsync(request));

    [HttpPatch("tasks/{id:long}/status/{status}")]
    public async Task<IActionResult> UpdateStatus(long id, HousekeepingTaskStatus status)
    {
        await housekeeping.UpdateTaskStatusAsync(id, status);
        return Wrap(new { id, status });
    }

    [HttpPatch("rooms/{roomId:long}/housekeeping")]
    public async Task<IActionResult> SetRoomStatus(long roomId, UpdateHousekeepingRequest request)
    {
        await housekeeping.SetRoomHousekeepingAsync(roomId, request);
        return Wrap(new { roomId, request.Status });
    }
}



