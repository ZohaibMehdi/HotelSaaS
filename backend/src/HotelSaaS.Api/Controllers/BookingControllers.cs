using HotelSaaS.Api.Controllers;
using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Bookings;
using HotelSaaS.Application.Dtos.Guests;
using HotelSaaS.Application.Services;
using HotelSaaS.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelSaaS.Api.Controllers;

[ApiController]
[Route("api/guests")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.TenantAdmin + "," + RoleNames.Manager + "," +
                   RoleNames.FrontDesk + "," + RoleNames.Finance)]
public class GuestsController(IGuestService guests) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null) =>
        Wrap(await guests.SearchAsync(page, pageSize, search));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id) => Wrap(await guests.GetByIdAsync(id));

    [HttpGet("{id:long}/history")]
    public async Task<IActionResult> History(long id) =>
        Wrap(await guests.HistoryAsync(id));

    [HttpPost]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager + "," + RoleNames.FrontDesk)]
    public async Task<IActionResult> Create(SaveGuestRequest request) =>
        Created(await guests.CreateAsync(request), "Guest created.");

    [HttpPut("{id:long}")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager + "," + RoleNames.FrontDesk)]
    public async Task<IActionResult> Update(long id, SaveGuestRequest request) =>
        Wrap(await guests.UpdateAsync(id, request));

    [HttpDelete("{id:long}")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Deactivate(long id)
    {
        await guests.DeactivateAsync(id);
        return NoContent();
    }
}

[ApiController]
[Route("api/bookings")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.TenantAdmin + "," + RoleNames.Manager + "," +
                   RoleNames.FrontDesk + "," + RoleNames.Finance)]
public class BookingsController(IBookingService bookings, IAvailabilityService availability) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] BookingStatus? status = null, [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null) =>
        Wrap(await bookings.SearchAsync(page, pageSize, search, status, from, to));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id) =>
        Wrap(await bookings.GetByIdAsync(id));

    /// <summary>PRD section 15 - availability check (authoritative re-check happens at booking time).</summary>
    [HttpGet("availability")]
    public async Task<IActionResult> Availability(
        [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] long? roomTypeId = null,
        [FromQuery] long? agreementId = null) =>
        Wrap(await availability.GetAsync(from, to, roomTypeId, null, agreementId));

    [HttpPost]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager + "," + RoleNames.FrontDesk)]
    public async Task<IActionResult> Create(CreateBookingRequest request) =>
        Created(await bookings.CreateAsync(request), "Reservation created and confirmed.");

    [HttpPut("{id:long}/dates")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager + "," + RoleNames.FrontDesk)]
    public async Task<IActionResult> UpdateDates(long id,
        [FromBody] UpdateDatesRequest request) =>
        Wrap(await bookings.UpdateDatesAsync(id, request.CheckInDate, request.CheckOutDate));

    /// <summary>PRD section 20 acceptance flow.</summary>
    [HttpPost("{id:long}/check-in")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager + "," + RoleNames.FrontDesk)]
    public async Task<IActionResult> CheckIn(long id)
    {
        await bookings.CheckInAsync(id);
        return Wrap(new { id, checkedIn = true }, "Guest checked in.");
    }

    /// <summary>Walk-in: creates a booking for today and checks it in atomically.</summary>
    [HttpPost("walk-in")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager + "," + RoleNames.FrontDesk)]
    public async Task<IActionResult> WalkIn(WalkInCheckInRequest request) =>
        Created(await bookings.WalkInCheckInAsync(request), "Walk-in guest checked in.");

    /// <summary>PRD section 21 acceptance flow; returns the generated invoice id.</summary>
    [HttpPost("{id:long}/check-out")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager + "," + RoleNames.FrontDesk)]
    public async Task<IActionResult> CheckOut(long id)
    {
        var invoiceId = await bookings.CheckOutAsync(id);
        return Wrap(new { id, invoiceId }, "Guest checked out. Invoice generated.");
    }

    [HttpPost("{id:long}/cancel")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager + "," + RoleNames.FrontDesk)]
    public async Task<IActionResult> Cancel(long id, CancelBookingRequest request)
    {
        await bookings.CancelAsync(id, request);
        return Wrap(new { id, cancelled = true });
    }

    [HttpPost("{id:long}/no-show")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager + "," + RoleNames.FrontDesk)]
    public async Task<IActionResult> NoShow(long id)
    {
        await bookings.MarkNoShowAsync(id);
        return Wrap(new { id, noShow = true });
    }

    [HttpGet("{id:long}/charges")]
    public async Task<IActionResult> Charges(long id) =>
        Wrap(await bookings.GetChargesAsync(id));

    [HttpPost("{id:long}/charges")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager + "," + RoleNames.FrontDesk)]
    public async Task<IActionResult> AddCharge(long id, AddChargeRequest request) =>
        Created(await bookings.AddChargeAsync(id, request));
}

public record UpdateDatesRequest(DateTime CheckInDate, DateTime CheckOutDate);



