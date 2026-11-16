using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.B2B;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Domain;
using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Application.Services;

public interface ICorporateCustomerService
{
    Task<PagedResult<CorporateCustomerDto>> SearchAsync(int page, int pageSize, string? search,
        CancellationToken ct = default);
    Task<CorporateCustomerDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<CorporateCustomerDto> CreateAsync(SaveCorporateCustomerRequest request, CancellationToken ct = default);
    Task<CorporateCustomerDto> UpdateAsync(long id, SaveCorporateCustomerRequest request, CancellationToken ct = default);
    Task SetStatusAsync(long id, CorporateStatus status, CancellationToken ct = default);
}

public class CorporateCustomerService(IAppDbContext db, ICurrentUser currentUser, IAuditService audit)
    : ICorporateCustomerService
{
    public async Task<PagedResult<CorporateCustomerDto>> SearchAsync(int page, int pageSize, string? search,
        CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var q = db.CorporateCustomers.AsNoTracking().Where(c => c.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(c => c.CompanyName.Contains(search) ||
                             (c.ContactPerson != null && c.ContactPerson.Contains(search)));
        q = q.OrderBy(c => c.CompanyName);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<CorporateCustomerDto>(items.Select(Map).ToList(), page, pageSize, total);
    }

    public async Task<CorporateCustomerDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        return Map(await FindAsync(id, tenantId, ct));
    }

    public async Task<CorporateCustomerDto> CreateAsync(SaveCorporateCustomerRequest request,
        CancellationToken ct = default)
    {
        Validate.Input(new SaveCorporateCustomerValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        if (await db.CorporateCustomers.AnyAsync(c =>
                c.TenantId == tenantId && c.IsActive && c.CompanyName == request.CompanyName.Trim(), ct))
            throw new ConflictException($"Corporate customer '{request.CompanyName}' already exists.");

        var corp = new CorporateCustomer
        {
            TenantId = tenantId,
            CompanyName = request.CompanyName.Trim(),
            RegistrationNumber = request.RegistrationNumber,
            TaxNumber = request.TaxNumber,
            ContactPerson = request.ContactPerson,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            BillingAddress = request.BillingAddress,
            Status = request.Status,
            Notes = request.Notes,
            CreatedBy = currentUser.UserId
        };
        db.CorporateCustomers.Add(corp);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Create", nameof(CorporateCustomer), corp.CorporateCustomerId,
            null, new { corp.CompanyName }, tenantId, ct);
        return Map(corp);
    }

    public async Task<CorporateCustomerDto> UpdateAsync(long id, SaveCorporateCustomerRequest request,
        CancellationToken ct = default)
    {
        Validate.Input(new SaveCorporateCustomerValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        var corp = await FindAsync(id, tenantId, ct);
        if (await db.CorporateCustomers.AnyAsync(c => c.TenantId == tenantId && c.IsActive &&
                c.CompanyName == request.CompanyName.Trim() && c.CorporateCustomerId != id, ct))
            throw new ConflictException($"Corporate customer '{request.CompanyName}' already exists.");

        corp.CompanyName = request.CompanyName.Trim();
        corp.RegistrationNumber = request.RegistrationNumber;
        corp.TaxNumber = request.TaxNumber;
        corp.ContactPerson = request.ContactPerson;
        corp.Phone = request.Phone;
        corp.Email = request.Email;
        corp.Address = request.Address;
        corp.BillingAddress = request.BillingAddress;
        corp.Status = request.Status;
        corp.Notes = request.Notes;
        corp.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Update", nameof(CorporateCustomer), id, null, new { corp.CompanyName }, tenantId, ct);
        return Map(corp);
    }

    public async Task SetStatusAsync(long id, CorporateStatus status, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var corp = await FindAsync(id, tenantId, ct);
        corp.Status = status;
        corp.UpdatedAt = DateTime.UtcNow;
        corp.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("SetStatus", nameof(CorporateCustomer), id, null, new { Status = status }, tenantId, ct);
    }

    private Task<CorporateCustomer> FindAsync(long id, long tenantId, CancellationToken ct) =>
        db.CorporateCustomers.FirstOrDefaultAsync(c => c.CorporateCustomerId == id && c.TenantId == tenantId, ct)
            .ContinueWith(t => t.Result ?? throw new NotFoundException("Corporate customer not found."), ct);

    internal static CorporateCustomerDto Map(CorporateCustomer c) => new(
        c.CorporateCustomerId, c.CompanyName, c.RegistrationNumber, c.TaxNumber, c.ContactPerson, c.Phone,
        c.Email, c.Address, c.BillingAddress, c.Status, c.Notes, c.IsActive);
}

public interface IAgreementService
{
    Task<IReadOnlyList<AgreementDto>> GetAllAsync(long? corporateCustomerId, AgreementStatus? status,
        CancellationToken ct = default);
    Task<AgreementDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<AgreementDto> CreateAsync(SaveAgreementRequest request, CancellationToken ct = default);
    Task<AgreementDto> UpdateAsync(long id, SaveAgreementRequest request, CancellationToken ct = default);
    Task SetStatusAsync(long id, AgreementStatus status, CancellationToken ct = default);
}

public class AgreementService(IAppDbContext db, ICurrentUser currentUser, IAuditService audit) : IAgreementService
{
    public async Task<IReadOnlyList<AgreementDto>> GetAllAsync(long? corporateCustomerId, AgreementStatus? status,
        CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var q = db.B2BAgreements.AsNoTracking()
            .Include(a => a.CorporateCustomer)
            .Include(a => a.Rates).ThenInclude(r => r.RoomType)
            .Where(a => a.TenantId == tenantId);
        if (corporateCustomerId.HasValue) q = q.Where(a => a.CorporateCustomerId == corporateCustomerId.Value);
        if (status.HasValue) q = q.Where(a => a.Status == status.Value);

        // Expire stale ACTIVE agreements whose end date has passed.
        var list = await q.OrderBy(a => a.AgreementNumber).ToListAsync(ct);
        foreach (var expired in list.Where(a => a.Status == AgreementStatus.Active && a.EndDate < DateOnly.FromDateTime(DateTime.UtcNow)).ToList())
        {
            expired.Status = AgreementStatus.Expired; // PRD rule 11: expired agreements never auto-apply
        }
        return list.Select(Map).ToList();
    }

    public async Task<AgreementDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        return Map(await FindFullAsync(id, tenantId, ct));
    }

    public async Task<AgreementDto> CreateAsync(SaveAgreementRequest request, CancellationToken ct = default)
    {
        Validate.Input(new SaveAgreementValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        await EnsureCorpAsync(request.CorporateCustomerId, tenantId, ct);
        if (await db.B2BAgreements.AnyAsync(a => a.TenantId == tenantId &&
                a.AgreementNumber == request.AgreementNumber.Trim(), ct))
            throw new ConflictException($"Agreement number '{request.AgreementNumber}' already exists.");

        await EnsureRoomTypesAsync(tenantId, request.Rates.Select(r => r.RoomTypeId), ct);

        var agreement = new B2BAgreement
        {
            TenantId = tenantId,
            CorporateCustomerId = request.CorporateCustomerId,
            AgreementNumber = request.AgreementNumber.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = request.Status,
            PaymentTermsDays = request.PaymentTermsDays,
            CreditLimit = request.CreditLimit,
            IncludesBreakfast = request.IncludesBreakfast,
            SpecialFacilities = request.SpecialFacilities,
            CancellationRules = request.CancellationRules,
            Notes = request.Notes,
            CreatedBy = currentUser.UserId
        };
        foreach (var r in request.Rates.GroupBy(x => x.RoomTypeId).Select(g => g.First()))
            agreement.Rates.Add(new B2BAgreementRate
            {
                TenantId = tenantId, RoomTypeId = r.RoomTypeId, ContractedRate = r.ContractedRate,
                ExtraAdultRate = r.ExtraAdultRate, ExtraChildRate = r.ExtraChildRate,
                IncludesBreakfast = r.IncludesBreakfast, TaxApplicable = r.TaxApplicable
            });
        db.B2BAgreements.Add(agreement);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Create", nameof(B2BAgreement), agreement.AgreementId,
            null, new { agreement.AgreementNumber }, tenantId, ct);
        return await GetByIdAsync(agreement.AgreementId, ct);
    }

    public async Task<AgreementDto> UpdateAsync(long id, SaveAgreementRequest request, CancellationToken ct = default)
    {
        Validate.Input(new SaveAgreementValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        var agreement = await FindFullAsync(id, tenantId, ct);
        await EnsureCorpAsync(request.CorporateCustomerId, tenantId, ct);
        if (agreement.Status is AgreementStatus.Expired or AgreementStatus.Cancelled)
            throw new BusinessRuleException("Expired or cancelled agreements cannot be edited.");
        await EnsureRoomTypesAsync(tenantId, request.Rates.Select(r => r.RoomTypeId), ct);

        agreement.CorporateCustomerId = request.CorporateCustomerId;
        agreement.StartDate = request.StartDate;
        agreement.EndDate = request.EndDate;
        agreement.Status = request.Status;
        agreement.PaymentTermsDays = request.PaymentTermsDays;
        agreement.CreditLimit = request.CreditLimit;
        agreement.IncludesBreakfast = request.IncludesBreakfast;
        agreement.SpecialFacilities = request.SpecialFacilities;
        agreement.CancellationRules = request.CancellationRules;
        agreement.Notes = request.Notes;
        agreement.UpdatedBy = currentUser.UserId;

        db.B2BAgreementRates.RemoveRange(agreement.Rates); // replace rate sheet wholesale
        agreement.Rates.Clear();
        foreach (var r in request.Rates.GroupBy(x => x.RoomTypeId).Select(g => g.First()))
            agreement.Rates.Add(new B2BAgreementRate
            {
                TenantId = tenantId, RoomTypeId = r.RoomTypeId, ContractedRate = r.ContractedRate,
                ExtraAdultRate = r.ExtraAdultRate, ExtraChildRate = r.ExtraChildRate,
                IncludesBreakfast = r.IncludesBreakfast, TaxApplicable = r.TaxApplicable
            });
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Update", nameof(B2BAgreement), id, null,
            new { agreement.AgreementNumber }, tenantId, ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task SetStatusAsync(long id, AgreementStatus status, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var agreement = await FindFullAsync(id, tenantId, ct);
        agreement.Status = status;
        agreement.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("SetStatus", nameof(B2BAgreement), id, null, new { Status = status }, tenantId, ct);
    }

    private Task<B2BAgreement> FindFullAsync(long id, long tenantId, CancellationToken ct) =>
        db.B2BAgreements.Include(a => a.CorporateCustomer)
            .Include(a => a.Rates).ThenInclude(r => r.RoomType)
            .FirstOrDefaultAsync(a => a.AgreementId == id && a.TenantId == tenantId, ct)
            .ContinueWith(t => t.Result ?? throw new NotFoundException("Agreement not found."), ct);

    private async Task EnsureCorpAsync(long corpId, long tenantId, CancellationToken ct)
    {
        if (!await db.CorporateCustomers.AnyAsync(c =>
                c.CorporateCustomerId == corpId && c.TenantId == tenantId, ct))
            throw new BadRequestException("Corporate customer not found in this property.");
    }

    private async Task EnsureRoomTypesAsync(long tenantId, IEnumerable<long> roomTypeIds, CancellationToken ct)
    {
        var ids = roomTypeIds.Distinct().ToList();
        var found = await db.RoomTypes.Where(rt => rt.TenantId == tenantId && ids.Contains(rt.RoomTypeId))
            .CountAsync(ct);
        if (found != ids.Count) throw new BadRequestException("One or more room types do not exist in this property.");
    }

    internal static AgreementDto Map(B2BAgreement a) => new(
        a.AgreementId, a.CorporateCustomerId, a.CorporateCustomer?.CompanyName ?? string.Empty,
        a.AgreementNumber, a.StartDate, a.EndDate, a.Status, a.PaymentTermsDays, a.CreditLimit,
        a.IncludesBreakfast, a.SpecialFacilities, a.CancellationRules, a.Notes,
        a.Rates.Select(r => new AgreementRateDto(r.AgreementRateId, r.RoomTypeId,
            r.RoomType?.Name ?? string.Empty, r.ContractedRate, r.ExtraAdultRate, r.ExtraChildRate,
            r.IncludesBreakfast, r.TaxApplicable)).ToList());
}
