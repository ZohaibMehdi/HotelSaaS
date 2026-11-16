using HotelSaaS.Api.Controllers;
using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Company;
using HotelSaaS.Application.Services;
using HotelSaaS.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelSaaS.Api.Controllers;

[ApiController]
[Route("api/company-information")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.TenantAdmin + "," + RoleNames.Manager + "," +
                   RoleNames.FrontDesk + "," + RoleNames.Finance)]
public class CompanyInformationController(ICompanyService company) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get() =>
        Wrap(await company.GetAsync());

    [HttpPut]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Save(SaveCompanyInformationRequest request) =>
        Wrap(await company.SaveAsync(request));
}

[ApiController]
[Route("api/property-settings")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.TenantAdmin + "," + RoleNames.Manager)]
public class PropertySettingsController(ICompanyService company) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get() =>
        Wrap(await company.GetSettingsAsync());

    [HttpPut]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager)]
    public async Task<IActionResult> Save(SavePropertySettingsRequest request) =>
        Wrap(await company.SaveSettingsAsync(request));
}



