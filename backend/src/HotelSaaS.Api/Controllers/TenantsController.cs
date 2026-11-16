using HotelSaaS.Api.Controllers;
using HotelSaaS.Application.Common;
using HotelSaaS.Application.Services;
using HotelSaaS.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelSaaS.Api.Controllers;

/// <summary>Platform administration - SuperAdmin only (PRD sections 6.1, 7.1, 10).</summary>
[ApiController]
[Route("api/tenants")]
[Authorize(Roles = RoleNames.SuperAdmin)]
public class TenantsController(ITenantService tenants) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] TenantStatus? status = null) =>
        Wrap(await tenants.SearchAsync(page, pageSize, search, status));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id) =>
        Wrap(await tenants.GetByIdAsync(id));

    /// <summary>Onboarding workflow: creates tenant + defaults + tenant admin (PRD section 10).</summary>
    [HttpPost("onboard")]
    public async Task<IActionResult> Onboard(CreateTenantRequest request) =>
        Created(await tenants.OnboardAsync(request), "Tenant onboarded successfully.");

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateTenantRequest request) =>
        Wrap(await tenants.UpdateAsync(id, request));

    [HttpPost("{id:long}/status/{status}")]
    public async Task<IActionResult> SetStatus(long id, TenantStatus status)
    {
        await tenants.SetStatusAsync(id, status);
        return Wrap(new { id, status });
    }
}



