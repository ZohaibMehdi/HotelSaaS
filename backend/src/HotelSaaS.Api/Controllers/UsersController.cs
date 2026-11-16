using HotelSaaS.Api.Controllers;
using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Auth;
using HotelSaaS.Application.Dtos.Users;
using HotelSaaS.Application.Services;
using HotelSaaS.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelSaaS.Api.Controllers;

/// <summary>Tenant user administration. SuperAdmin may manage platform users with ?tenantId=</summary>
[ApiController]
[Route("api/users")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.TenantAdmin)]
public class UsersController(IUserService users, ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] long? tenantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null) =>
        Wrap(await users.SearchAsync(TenantScope.Optional(currentUser, tenantId), page, pageSize, search));

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest request) =>
        Created(await users.CreateAsync(request), "User created.");

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateUserRequest request) =>
        Wrap(await users.UpdateAsync(id, request));

    [HttpPost("{id:long}/reset-password")]
    public async Task<IActionResult> ResetPassword(long id, AdminResetPasswordRequest request)
    {
        await users.ResetPasswordAsync(id, request);
        return Wrap(new { id }, "Temporary password assigned; the user must change it at next login.");
    }

    [HttpPost("{id:long}/active/{isActive:bool}")]
    public async Task<IActionResult> SetActive(long id, bool isActive)
    {
        await users.SetActiveAsync(id, isActive);
        return Wrap(new { id, isActive });
    }
}



