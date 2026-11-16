using FluentValidation;
using HotelSaaS.Application.Common;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Application.Security;
using HotelSaaS.Domain;
using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Application.Services;

public record TenantDto(long TenantId, string TenantCode, string BusinessName, string? LegalName,
    TenantStatus Status, string ContactEmail, string? ContactPhone, string? Address, string? City,
    string? Country, string TimeZone, string Currency, DateTime CreatedAt, bool IsActive);

public record CreateTenantRequest(string TenantCode, string BusinessName, string? LegalName,
    string ContactEmail, string? ContactPhone, string? Address, string? City, string? Country,
    string TimeZone, string Currency,
    string AdminFullName, string AdminEmail, string AdminPhone, string InitialPassword);

public class CreateTenantValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantValidator()
    {
        RuleFor(x => x.TenantCode).NotEmpty().Matches("^[A-Za-z0-9_-]{2,30}$")
            .WithMessage("Tenant code must be 2-30 letters/digits/hyphen/underscore.");
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Currency).Length(3);
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(100);
        RuleFor(x => x.AdminFullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.InitialPassword).NotEmpty().MinimumLength(8);
    }
}

public record UpdateTenantRequest(string BusinessName, string? LegalName, string ContactEmail,
    string? ContactPhone, string? Address, string? City, string? Country, string TimeZone, string Currency);

public class UpdateTenantValidator : AbstractValidator<UpdateTenantRequest>
{
    public UpdateTenantValidator()
    {
        RuleFor(x => x.BusinessName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.Currency).Length(3);
    }
}

public interface ITenantService
{
    Task<PagedResult<TenantDto>> SearchAsync(int page, int pageSize, string? search, TenantStatus? status,
        CancellationToken ct = default);
    Task<TenantDto> GetByIdAsync(long tenantId, CancellationToken ct = default);
    Task<TenantDto> OnboardAsync(CreateTenantRequest request, CancellationToken ct = default);
    Task<TenantDto> UpdateAsync(long tenantId, UpdateTenantRequest request, CancellationToken ct = default);
    Task SetStatusAsync(long tenantId, TenantStatus status, CancellationToken ct = default);
}

public class TenantService(IAppDbContext db, ICurrentUser currentUser, IAuditService audit) : ITenantService
{
    public async Task<PagedResult<TenantDto>> SearchAsync(int page, int pageSize, string? search,
        TenantStatus? status, CancellationToken ct = default)
    {
        var q = db.Tenants.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(t => t.BusinessName.Contains(search) || t.TenantCode.Contains(search) ||
                             t.ContactEmail.Contains(search));
        if (status.HasValue) q = q.Where(t => t.Status == status.Value);
        q = q.OrderByDescending(t => t.CreatedAt);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<TenantDto>(items.Select(Map).ToList(), page, pageSize, total);
    }

    public async Task<TenantDto> GetByIdAsync(long tenantId, CancellationToken ct = default)
    {
        var t = await FindAccessibleAsync(tenantId, ct);
        return Map(t);
    }

    /// <summary>PRD section 10 onboarding workflow: tenant + defaults + admin user in one transaction.</summary>
    public async Task<TenantDto> OnboardAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        Validate.Input(new CreateTenantValidator(), request);
        var code = request.TenantCode.ToUpperInvariant();

        if (await db.Tenants.AnyAsync(t => t.TenantCode == code, ct))
            throw new ConflictException($"Tenant code '{code}' is already in use.");

        var adminEmail = request.AdminEmail.Trim().ToLowerInvariant();

        await db.BeginTransactionAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            var tenant = new Tenant
            {
                TenantCode = code,
                BusinessName = request.BusinessName.Trim(),
                LegalName = request.LegalName?.Trim(),
                Status = TenantStatus.Active,
                ContactEmail = request.ContactEmail.Trim(),
                ContactPhone = request.ContactPhone,
                Address = request.Address,
                City = request.City,
                Country = request.Country,
                TimeZone = request.TimeZone,
                Currency = request.Currency.ToUpperInvariant(),
                CreatedBy = currentUser.UserId,
                CreatedAt = now
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(ct); // need TenantId

            // Operational defaults so a newly onboarded tenant can work immediately.
            db.PropertySettings.Add(new PropertySettings
            {
                TenantId = tenant.TenantId,
                PropertyName = tenant.BusinessName,
                CreatedAt = now,
                CreatedBy = currentUser.UserId
            });
            db.CompanyInformations.Add(new CompanyInformation
            {
                TenantId = tenant.TenantId,
                BusinessName = tenant.BusinessName,
                Email = tenant.ContactEmail,
                Phone = tenant.ContactPhone,
                Address = tenant.Address,
                City = tenant.City,
                Country = tenant.Country,
                CreatedAt = now,
                CreatedBy = currentUser.UserId
            });

            var admin = new User
            {
                TenantId = tenant.TenantId,
                FullName = request.AdminFullName.Trim(),
                Email = adminEmail,
                Phone = request.AdminPhone,
                PasswordHash = PasswordHasher.Hash(request.InitialPassword),
                CreatedBy = currentUser.UserId,
                CreatedAt = now
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync(ct);

            db.UserRoles.Add(new UserRole { UserId = admin.UserId, RoleId = 2 }); // TenantAdmin

            await db.SaveChangesAsync(ct);
            await db.CommitTransactionAsync(ct);

            await audit.LogAsync("Onboard", nameof(Tenant), tenant.TenantId,
                null, new { tenant.TenantCode, tenant.BusinessName }, tenant.TenantId, ct);
            await audit.LogAsync("Create", nameof(User), admin.UserId, null,
                new { admin.Email, Role = RoleNames.TenantAdmin }, tenant.TenantId, ct);

            return Map(tenant);
        }
        catch
        {
            await db.RollbackTransactionAsync(ct);
            throw;
        }
    }

    public async Task<TenantDto> UpdateAsync(long tenantId, UpdateTenantRequest request, CancellationToken ct = default)
    {
        Validate.Input(new UpdateTenantValidator(), request);
        var tenant = await FindAccessibleAsync(tenantId, ct);
        var old = new { tenant.BusinessName, tenant.Status };

        tenant.BusinessName = request.BusinessName.Trim();
        tenant.LegalName = request.LegalName?.Trim();
        tenant.ContactEmail = request.ContactEmail.Trim();
        tenant.ContactPhone = request.ContactPhone;
        tenant.Address = request.Address;
        tenant.City = request.City;
        tenant.Country = request.Country;
        tenant.TimeZone = request.TimeZone;
        tenant.Currency = request.Currency.ToUpperInvariant();
        tenant.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);

        await audit.LogAsync("Update", nameof(Tenant), tenant.TenantId, old,
            new { tenant.BusinessName, tenant.Status }, tenant.TenantId, ct);
        return Map(tenant);
    }

    /// <summary>Activate / deactivate / suspend a tenant. Inactive tenants block all their logins.</summary>
    public async Task SetStatusAsync(long tenantId, TenantStatus status, CancellationToken ct = default)
    {
        var tenant = await FindAccessibleAsync(tenantId, ct);
        if (tenant.Status == status) return;
        var old = tenant.Status;
        tenant.Status = status;
        tenant.IsActive = status is TenantStatus.Active or TenantStatus.Pending;
        tenant.UpdatedAt = DateTime.UtcNow;
        tenant.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("SetStatus", nameof(Tenant), tenant.TenantId,
            new { Status = old }, new { Status = status }, tenant.TenantId, ct);
    }

    private async Task<Tenant> FindAccessibleAsync(long tenantId, CancellationToken ct)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.TenantId == tenantId, ct)
                     ?? throw new NotFoundException("Tenant not found.");
        if (!currentUser.IsSuperAdmin && currentUser.TenantId != tenant.TenantId)
            throw new ForbiddenException(); // cross-tenant access denied
        return tenant;
    }

    private static TenantDto Map(Tenant t) => new(
        t.TenantId, t.TenantCode, t.BusinessName, t.LegalName, t.Status, t.ContactEmail, t.ContactPhone,
        t.Address, t.City, t.Country, t.TimeZone, t.Currency, t.CreatedAt, t.IsActive);
}
