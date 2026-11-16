using HotelSaaS.Api.Controllers;
using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Rooms;
using HotelSaaS.Application.Services;
using HotelSaaS.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelSaaS.Api.Controllers;

[ApiController]
[Route("api/room-types")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.TenantAdmin + "," + RoleNames.Manager + "," +
                   RoleNames.FrontDesk + "," + RoleNames.Finance)]
public class RoomTypesController(IRoomTypeService roomTypes) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeInactive = false) =>
        Wrap(await roomTypes.GetAllAsync(includeInactive));

    [HttpPost]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Create(SaveRoomTypeRequest request) =>
        Created(await roomTypes.CreateAsync(request));

    [HttpPut("{id:long}")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Update(long id, SaveRoomTypeRequest request) =>
        Wrap(await roomTypes.UpdateAsync(id, request));

    [HttpDelete("{id:long}")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Deactivate(long id)
    {
        await roomTypes.DeactivateAsync(id);
        return NoContent();
    }
}

/// <summary>
/// Room management. Housekeeping role can update housekeeping status only
/// (PRD section 7.7 / data ownership matrix).
/// </summary>
[ApiController]
[Route("api/rooms")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.TenantAdmin + "," + RoleNames.Manager + "," +
                   RoleNames.FrontDesk + "," + RoleNames.Finance + "," + RoleNames.Housekeeping)]
public class RoomsController(IRoomService rooms) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? search = null,
        [FromQuery] long? roomTypeId = null, [FromQuery] RoomStatus? status = null) =>
        Wrap(await rooms.SearchAsync(page, pageSize, search, roomTypeId, status));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id)
    {
        var result = await rooms.SearchAsync(1, 1, null, null, null);
        var room = result.Items.FirstOrDefault(r => r.RoomId == id)
                   ?? throw new NotFoundException("Room not found.");
        return Wrap(room);
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Create(SaveRoomRequest request) =>
        Created(await rooms.CreateAsync(request));

    [HttpPut("{id:long}")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Update(long id, SaveRoomRequest request) =>
        Wrap(await rooms.UpdateAsync(id, request));

    /// <summary>Operational status changes (front desk / managers).</summary>
    [HttpPatch("{id:long}/status")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager + "," + RoleNames.FrontDesk)]
    public async Task<IActionResult> UpdateStatus(long id, UpdateRoomStatusRequest request)
    {
        await rooms.UpdateStatusAsync(id, request);
        return Wrap(new { id, request.Status });
    }

    /// <summary>Housekeeping lifecycle updates (housekeeping staff).</summary>
    [HttpPatch("{id:long}/housekeeping")]
    [Authorize(Roles = RoleNames.Housekeeping + "," + RoleNames.TenantAdmin + "," + RoleNames.Manager)]
    public async Task<IActionResult> UpdateHousekeeping(long id, UpdateHousekeepingRequest request)
    {
        await rooms.UpdateHousekeepingAsync(id, request);
        return Wrap(new { id, request.Status });
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Deactivate(long id)
    {
        await rooms.DeactivateAsync(id);
        return NoContent();
    }
}



