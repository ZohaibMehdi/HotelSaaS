using HotelSaaS.Api.Controllers;
using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Ops;
using HotelSaaS.Application.Services;
using HotelSaaS.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelSaaS.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.TenantAdmin + "," + RoleNames.Manager + "," +
                   RoleNames.Hr + "," + RoleNames.Finance)]
public class EmployeesController(IEmployeeService employees) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] long? departmentId = null, [FromQuery] EmploymentStatus? status = null) =>
        Wrap(await employees.SearchAsync(page, pageSize, search, departmentId, status));

    [HttpGet("departments")]
    public async Task<IActionResult> Departments() =>
        Wrap(await employees.GetDepartmentsAsync());

    [HttpPost("departments")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager + "," + RoleNames.Hr)]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentRequest request) =>
        Created(await employees.CreateDepartmentAsync(request.Name));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id) =>
        Wrap(await employees.GetByIdAsync(id));

    [HttpPost]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager + "," + RoleNames.Hr)]
    public async Task<IActionResult> Create(SaveEmployeeRequest request) =>
        Created(await employees.CreateAsync(request), "Employee created.");

    [HttpPut("{id:long}")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Manager + "," + RoleNames.Hr)]
    public async Task<IActionResult> Update(long id, SaveEmployeeRequest request) =>
        Wrap(await employees.UpdateAsync(id, request));

    [HttpDelete("{id:long}")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Hr)]
    public async Task<IActionResult> Terminate(long id)
    {
        await employees.DeactivateAsync(id);
        return NoContent();
    }
}

public record CreateDepartmentRequest(string Name);

/// <summary>PRD section 30 - payroll with immutable finalized runs.</summary>
[ApiController]
[Route("api/payroll")]
[Authorize(Roles = RoleNames.SuperAdmin + "," + RoleNames.TenantAdmin + "," + RoleNames.Hr + "," + RoleNames.Finance)]
public class PayrollController(IPayrollService payroll) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Wrap(await payroll.GetAllAsync());

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id) => Wrap(await payroll.GetByIdAsync(id));

    [HttpPost]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Hr)]
    public async Task<IActionResult> Create(CreatePayrollRunRequest request) =>
        Created(await payroll.CreateRunAsync(request), "Draft payroll run created.");

    [HttpPost("{id:long}/generate")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Hr)]
    public async Task<IActionResult> GenerateItems(long id) =>
        Wrap(await payroll.GenerateItemsAsync(id));

    [HttpPost("{id:long}/items")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Hr)]
    public async Task<IActionResult> UpsertItem(long id, UpsertPayrollItemRequest request) =>
        Wrap(await payroll.UpsertItemAsync(id, request));

    [HttpDelete("{runId:long}/items/{itemId:long}")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Hr)]
    public async Task<IActionResult> RemoveItem(long runId, long itemId)
    {
        await payroll.RemoveItemAsync(runId, itemId);
        return NoContent();
    }

    /// <summary>Finalizes the run - becomes immutable (PRD rule 12).</summary>
    [HttpPost("{id:long}/finalize")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Hr)]
    public async Task<IActionResult> Finalize(long id) =>
        Wrap(await payroll.FinalizeAsync(id));

    [HttpPost("{id:long}/mark-paid")]
    [Authorize(Roles = RoleNames.TenantAdmin + "," + RoleNames.Hr + "," + RoleNames.Finance)]
    public async Task<IActionResult> MarkPaid(long id) =>
        Wrap(await payroll.MarkPaidAsync(id));
}



