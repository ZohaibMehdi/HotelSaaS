using ValidationException = HotelSaaS.Application.Common.ValidationException;
using HotelSaaS.Application.Dtos.Ops;
using FluentValidation;
using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Ops;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Domain;
using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Application.Services;

public interface IEmployeeService
{
    Task<PagedResult<EmployeeDto>> SearchAsync(int page, int pageSize, string? search, long? departmentId,
        EmploymentStatus? status, CancellationToken ct = default);
    Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken ct = default);
    Task<DepartmentDto> CreateDepartmentAsync(string name, CancellationToken ct = default);
    Task<EmployeeDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<EmployeeDto> CreateAsync(SaveEmployeeRequest request, CancellationToken ct = default);
    Task<EmployeeDto> UpdateAsync(long id, SaveEmployeeRequest request, CancellationToken ct = default);
    Task DeactivateAsync(long id, CancellationToken ct = default);
}

public record EmployeeDto(long EmployeeId, string EmployeeCode, string FirstName, string? LastName, string? Cnic,
    string? Phone, string? Email, long? DepartmentId, string? DepartmentName, string? Designation,
    DateOnly JoiningDate, EmploymentStatus EmploymentStatus, decimal BasicSalary, string? BankName,
    string? BankAccountNumber, string? EmergencyContactName, string? EmergencyContactPhone,
    DateOnly? TerminationDate, bool IsActive);

public record SaveEmployeeRequest(string EmployeeCode, string FirstName, string? LastName, string? Cnic,
    string? Phone, string? Email, long? DepartmentId, string? Designation, DateOnly JoiningDate,
    EmploymentStatus EmploymentStatus, decimal BasicSalary, string? BankName, string? BankAccountNumber,
    string? EmergencyContactName, string? EmergencyContactPhone, DateOnly? TerminationDate);

public class SaveEmployeeValidator : AbstractValidator<SaveEmployeeRequest>
{
    public SaveEmployeeValidator()
    {
        RuleFor(x => x.EmployeeCode).NotEmpty().MaximumLength(30);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(80);
        RuleFor(x => x.JoiningDate).LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Joining date cannot be in the future.");
        RuleFor(x => x.BasicSalary).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.EmploymentStatus).IsInEnum();
    }
}

public record DepartmentDto(long DepartmentId, string Name);

public class EmployeeService(IAppDbContext db, ICurrentUser currentUser, IAuditService audit) : IEmployeeService
{
    public async Task<PagedResult<EmployeeDto>> SearchAsync(int page, int pageSize, string? search,
        long? departmentId, EmploymentStatus? status, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var q = db.Employees.AsNoTracking().Include(e => e.Department)
            .Where(e => e.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(e => e.FirstName.Contains(search) || (e.LastName != null && e.LastName.Contains(search)) ||
                             e.EmployeeCode.Contains(search));
        if (departmentId.HasValue) q = q.Where(e => e.DepartmentId == departmentId.Value);
        if (status.HasValue) q = q.Where(e => e.EmploymentStatus == status.Value);
        q = q.OrderBy(e => e.EmployeeCode);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<EmployeeDto>(items.Select(Map).ToList(), page, pageSize, total);
    }

    public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        return (await db.Departments.AsNoTracking()
                .Where(d => d.TenantId == tenantId && d.IsActive)
                .OrderBy(d => d.Name).ToListAsync(ct))
            .Select(d => new DepartmentDto(d.DepartmentId, d.Name)).ToList();
    }

    public async Task<DepartmentDto> CreateDepartmentAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Validation failed.",
                new Dictionary<string, string[]> { ["name"] = ["Department name is required."] });
        var tenantId = currentUser.RequireTenantId();
        name = name.Trim();
        if (await db.Departments.AnyAsync(d => d.TenantId == tenantId && d.IsActive && d.Name == name, ct))
            throw new ConflictException($"Department '{name}' already exists.");
        var dept = new Department { TenantId = tenantId, Name = name, CreatedAt = DateTime.UtcNow };
        db.Departments.Add(dept);
        await db.SaveChangesAsync(ct);
        return new DepartmentDto(dept.DepartmentId, dept.Name);
    }

    public async Task<EmployeeDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        return Map(await FindAsync(id, tenantId, ct));
    }

    public async Task<EmployeeDto> CreateAsync(SaveEmployeeRequest request, CancellationToken ct = default)
    {
        Validate.Input(new SaveEmployeeValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        if (await db.Employees.AnyAsync(e =>
                e.TenantId == tenantId && e.IsActive && e.EmployeeCode == request.EmployeeCode.Trim(), ct))
            throw new ConflictException($"Employee code '{request.EmployeeCode}' already exists.");
        if (request.TerminationDate.HasValue && request.TerminationDate < request.JoiningDate)
            throw new BusinessRuleException("Termination date cannot precede joining date.");

        var emp = MapToEntity(request, null);
        emp.TenantId = tenantId;
        emp.CreatedBy = currentUser.UserId;
        db.Employees.Add(emp);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Create", nameof(Employee), emp.EmployeeId, null,
            new { emp.EmployeeCode, emp.BasicSalary }, tenantId, ct);
        return Map(emp);
    }

    public async Task<EmployeeDto> UpdateAsync(long id, SaveEmployeeRequest request, CancellationToken ct = default)
    {
        Validate.Input(new SaveEmployeeValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        var emp = await FindAsync(id, tenantId, ct);
        if (await db.Employees.AnyAsync(e => e.TenantId == tenantId && e.IsActive &&
                e.EmployeeCode == request.EmployeeCode.Trim() && e.EmployeeId != id, ct))
            throw new ConflictException($"Employee code '{request.EmployeeCode}' already exists.");

        var old = new { emp.BasicSalary, emp.EmploymentStatus };
        MapToEntity(request, emp);
        emp.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Update", nameof(Employee), id, old,
            new { emp.BasicSalary, emp.EmploymentStatus }, tenantId, ct);
        return Map(emp);
    }

    public async Task DeactivateAsync(long id, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var emp = await FindAsync(id, tenantId, ct);
        emp.IsActive = false;
        emp.EmploymentStatus = EmploymentStatus.Terminated;
        emp.TerminationDate = DateOnly.FromDateTime(DateTime.UtcNow);
        emp.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Deactivate", nameof(Employee), id, null, null, tenantId, ct);
    }

    private static Employee MapToEntity(SaveEmployeeRequest r, Employee? existing)
    {
        var e = existing ?? new Employee();
        e.EmployeeCode = r.EmployeeCode.Trim();
        e.FirstName = r.FirstName.Trim();
        e.LastName = r.LastName?.Trim();
        e.Cnic = r.Cnic;
        e.Phone = r.Phone;
        e.Email = r.Email;
        e.DepartmentId = r.DepartmentId;
        e.Designation = r.Designation;
        e.JoiningDate = r.JoiningDate;
        e.EmploymentStatus = r.EmploymentStatus;
        e.BasicSalary = r.BasicSalary;
        e.BankName = r.BankName;
        e.BankAccountNumber = r.BankAccountNumber;
        e.EmergencyContactName = r.EmergencyContactName;
        e.EmergencyContactPhone = r.EmergencyContactPhone;
        e.TerminationDate = r.TerminationDate;
        return e;
    }

    private Task<Employee> FindAsync(long id, long tenantId, CancellationToken ct) =>
        db.Employees.Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.EmployeeId == id && e.TenantId == tenantId, ct)
            .ContinueWith(t => t.Result ?? throw new NotFoundException("Employee not found."), ct);

    internal static EmployeeDto Map(Employee e) => new(
        e.EmployeeId, e.EmployeeCode, e.FirstName, e.LastName, e.Cnic, e.Phone, e.Email,
        e.DepartmentId, e.Department?.Name, e.Designation, e.JoiningDate, e.EmploymentStatus, e.BasicSalary,
        e.BankName, e.BankAccountNumber, e.EmergencyContactName, e.EmergencyContactPhone,
        e.TerminationDate, e.IsActive);
}


