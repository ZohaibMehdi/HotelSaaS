using HotelSaaS.Api.Controllers;
using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.B2B;
using HotelSaaS.Application.Services;
using HotelSaaS.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelSaaS.Api.Controllers;

[ApiController]
[Route("api/corporate-customers")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.TenantAdmin + "," + RoleNames.Manager + "," +
                   RoleNames.FrontDesk + "," + RoleNames.Finance)]
public class CorporateCustomersController(ICorporateCustomerService customers) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null) =>
        Wrap(await customers.SearchAsync(page, pageSize, search));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id) =>
        Wrap(await customers.GetByIdAsync(id));

    [HttpPost]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Create(SaveCorporateCustomerRequest request) =>
        Created(await customers.CreateAsync(request));

    [HttpPut("{id:long}")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Update(long id,
        SaveCorporateCustomerRequest request) =>
        Wrap(await customers.UpdateAsync(id, request));

    [HttpPost("{id:long}/status/{status}")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager)]
    public async Task<IActionResult> SetStatus(long id, CorporateStatus status)
    {
        await customers.SetStatusAsync(id, status);
        return Wrap(new { id, status });
    }
}

/// <summary>B2B agreements with contracted room-type rates (PRD sections 26-28).</summary>
[ApiController]
[Route("api/b2b-agreements")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.TenantAdmin + "," + RoleNames.Manager + "," +
                   RoleNames.FrontDesk + "," + RoleNames.Finance)]
public class B2BAgreementsController(IAgreementService agreements) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] long? corporateCustomerId = null, [FromQuery] AgreementStatus? status = null) =>
        Wrap(await agreements.GetAllAsync(corporateCustomerId, status));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id) =>
        Wrap(await agreements.GetByIdAsync(id));

    [HttpPost]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Create(SaveAgreementRequest request) =>
        Created(await agreements.CreateAsync(request));

    [HttpPut("{id:long}")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Update(long id, SaveAgreementRequest request) =>
        Wrap(await agreements.UpdateAsync(id, request));

    [HttpPost("{id:long}/status/{status}")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager)]
    public async Task<IActionResult> SetStatus(long id, AgreementStatus status)
    {
        await agreements.SetStatusAsync(id, status);
        return Wrap(new { id, status });
    }
}



