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

public record PayrollRunDto(long PayrollRunId, DateOnly PayrollMonth, DateOnly PeriodStart, DateOnly PeriodEnd,
    PayrollRunStatus Status, decimal TotalGross, decimal TotalDeductions, decimal TotalNet,
    DateTime? FinalizedAt, DateTime? PaidAt, string? Notes, IReadOnlyList<PayrollItemDto> Items);

public record PayrollItemDto(long PayrollItemId, long EmployeeId, string EmployeeName, string EmployeeCode,
    string? Department, decimal BasicSalary, decimal Allowances, decimal OvertimeAmount, decimal BonusAmount,
    decimal Deductions, decimal AdvanceDeduction, decimal LoanDeduction, decimal AbsenceDeduction,
    decimal GrossSalary, decimal NetSalary, string? Remarks);

public record CreatePayrollRunRequest(int Year, int Month);

public class CreatePayrollRunValidator : AbstractValidator<CreatePayrollRunRequest>
{
    public CreatePayrollRunValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public record UpsertPayrollItemRequest(long EmployeeId, decimal Allowances = 0, decimal OvertimeAmount = 0,
    decimal BonusAmount = 0, decimal Deductions = 0, decimal AdvanceDeduction = 0, decimal LoanDeduction = 0,
    decimal AbsenceDeduction = 0, string? Remarks = null);

public class UpsertPayrollItemValidator : AbstractValidator<UpsertPayrollItemRequest>
{
    public UpsertPayrollItemValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.Allowances).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OvertimeAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.BonusAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Deductions).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AdvanceDeduction).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LoanDeduction).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AbsenceDeduction).GreaterThanOrEqualTo(0);
    }
}

public interface IPayrollService
{
    Task<IReadOnlyList<PayrollRunDto>> GetAllAsync(CancellationToken ct = default);
    Task<PayrollRunDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<PayrollRunDto> CreateRunAsync(CreatePayrollRunRequest request, CancellationToken ct = default);
    /// <summary>Adds draft items for all active employees not yet in the run.</summary>
    Task<PayrollRunDto> GenerateItemsAsync(long runId, CancellationToken ct = default);
    Task<PayrollRunDto> UpsertItemAsync(long runId, UpsertPayrollItemRequest request, CancellationToken ct = default);
    Task RemoveItemAsync(long runId, long itemId, CancellationToken ct = default);
    Task<PayrollRunDto> FinalizeAsync(long runId, CancellationToken ct = default);
    Task<PayrollRunDto> MarkPaidAsync(long runId, CancellationToken ct = default);
}

/// <summary>PRD section 30: finalized payroll is immutable; corrections happen via a new run/adjustment.</summary>
public class PayrollService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAuditService audit,
    INotificationService notifications) : IPayrollService
{
    public async Task<IReadOnlyList<PayrollRunDto>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var run = (await db.PayrollRuns.AsNoTracking().Where(r => r.TenantId == tenantId)
                .OrderByDescending(r => r.PayrollMonth).ToListAsync(ct))
            .Select(r => new PayrollRunDto(r.PayrollRunId, r.PayrollMonth, r.PeriodStart, r.PeriodEnd,
                r.Status, r.TotalGross, r.TotalDeductions, r.TotalNet, r.FinalizedAt, r.PaidAt, r.Notes, []))
            .ToList();
        return run;
    }

    public async Task<PayrollRunDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var (run, items) = await FindFullAsync(id, ct);
        return new PayrollRunDto(run.PayrollRunId, run.PayrollMonth, run.PeriodStart, run.PeriodEnd,
            run.Status, run.TotalGross, run.TotalDeductions, run.TotalNet, run.FinalizedAt, run.PaidAt,
            run.Notes, items);
    }

    public async Task<PayrollRunDto> CreateRunAsync(CreatePayrollRunRequest request, CancellationToken ct = default)
    {
        Validate.Input(new CreatePayrollRunValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        var monthStart = new DateOnly(request.Year, request.Month, 1);
        if (await db.PayrollRuns.AnyAsync(r => r.TenantId == tenantId && r.IsActive && r.PayrollMonth == monthStart, ct))
            throw new ConflictException($"A payroll run for {request.Year:0000}-{request.Month:00} already exists.");

        var run = new PayrollRun
        {
            TenantId = tenantId,
            PayrollMonth = monthStart,
            PeriodStart = monthStart,
            PeriodEnd = monthStart.AddMonths(1).AddDays(-1),
            CreatedBy = currentUser.UserId
        };
        db.PayrollRuns.Add(run);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Create", nameof(PayrollRun), run.PayrollRunId, null,
            new { Month = $"{request.Year}-{request.Month}" }, tenantId, ct);
        return await GetByIdAsync(run.PayrollRunId, ct);
    }

    public async Task<PayrollRunDto> GenerateItemsAsync(long runId, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r =>
            r.PayrollRunId == runId && r.TenantId == tenantId, ct) ?? throw new NotFoundException("Payroll run not found.");
        EnsureDraft(run);

        var existing = await db.PayrollItems.Where(i => i.PayrollRunId == runId)
            .Select(i => i.EmployeeId).ToListAsync(ct);
        var actives = await db.Employees
            .Where(e => e.TenantId == tenantId && e.IsActive &&
                        e.EmploymentStatus == EmploymentStatus.Active || e.EmploymentStatus == EmploymentStatus.Probation || e.EmploymentStatus == EmploymentStatus.OnLeave)
            .ToListAsync(ct);

        foreach (var emp in actives.Where(e => !existing.Contains(e.EmployeeId)))
            db.PayrollItems.Add(BuildItem(run, emp));
        RecalculateTotals(run);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(runId, ct);
    }

    public async Task<PayrollRunDto> UpsertItemAsync(long runId, UpsertPayrollItemRequest request,
        CancellationToken ct = default)
    {
        Validate.Input(new UpsertPayrollItemValidator(), request);
        var tenantId = currentUser.RequireTenantId();
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r =>
            r.PayrollRunId == runId && r.TenantId == tenantId, ct) ?? throw new NotFoundException("Payroll run not found.");
        EnsureDraft(run);

        var emp = await db.Employees.FirstOrDefaultAsync(e =>
            e.EmployeeId == request.EmployeeId && e.TenantId == tenantId, ct)
            ?? throw new BadRequestException("Employee not found in this property.");

        var item = await db.PayrollItems
            .FirstOrDefaultAsync(i => i.PayrollRunId == runId && i.EmployeeId == request.EmployeeId, ct);
        if (item is null)
        {
            item = BuildItem(run, emp);
            db.PayrollItems.Add(item);
        }
        item.Allowances = request.Allowances;
        item.OvertimeAmount = request.OvertimeAmount;
        item.BonusAmount = request.BonusAmount;
        item.Deductions = request.Deductions;
        item.AdvanceDeduction = request.AdvanceDeduction;
        item.LoanDeduction = request.LoanDeduction;
        item.AbsenceDeduction = request.AbsenceDeduction;
        item.Remarks = request.Remarks;
        Compute(item);

        RecalculateTotals(run);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(runId, ct);
    }

    public async Task RemoveItemAsync(long runId, long itemId, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r =>
            r.PayrollRunId == runId && r.TenantId == tenantId, ct) ?? throw new NotFoundException("Payroll run not found.");
        EnsureDraft(run);
        var item = await db.PayrollItems.FirstOrDefaultAsync(i =>
            i.PayrollItemId == itemId && i.PayrollRunId == runId, ct)
            ?? throw new NotFoundException("Payroll item not found.");
        db.PayrollItems.Remove(item);
        RecalculateTotals(run);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PayrollRunDto> FinalizeAsync(long runId, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var run = await db.PayrollRuns.Include(r => r.Items).FirstOrDefaultAsync(r =>
            r.PayrollRunId == runId && r.TenantId == tenantId, ct)
            ?? throw new NotFoundException("Payroll run not found.");
        EnsureDraft(run);
        if (run.Items.Count == 0)
            throw new BusinessRuleException("Cannot finalize an empty payroll run.");

        RecalculateTotals(run);
        run.Status = PayrollRunStatus.Finalized;
        run.FinalizedAt = DateTime.UtcNow;
        run.FinalizedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Finalize", nameof(PayrollRun), runId, null,
            new { run.TotalNet, Employees = run.Items.Count }, tenantId, ct);
        await notifications.CreateInAppAsync(null, tenantId,
            "Payroll finalized",
            $"Payroll for {run.PayrollMonth:yyyy-MM} finalized. Net total {run.TotalNet:N2}.",
            NotificationType.PayrollCompleted, ct);
        return await GetByIdAsync(runId, ct);
    }

    public async Task<PayrollRunDto> MarkPaidAsync(long runId, CancellationToken ct = default)
    {
        var tenantId = currentUser.RequireTenantId();
        var run = await db.PayrollRuns.FirstOrDefaultAsync(r =>
            r.PayrollRunId == runId && r.TenantId == tenantId, ct) ?? throw new NotFoundException("Payroll run not found.");
        if (run.Status != PayrollRunStatus.Finalized)
            throw new BusinessRuleException("Only finalized payroll runs can be marked as paid.");
        run.Status = PayrollRunStatus.Paid;
        run.PaidAt = DateTime.UtcNow;
        run.UpdatedBy = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("MarkPaid", nameof(PayrollRun), runId, null, new { run.PaidAt }, tenantId, ct);
        return await GetByIdAsync(runId, ct);
    }

    // ------------------------------------------------------------------ core

    private static void EnsureDraft(PayrollRun run)
    {
        if (run.Status != PayrollRunStatus.Draft)
            throw new BusinessRuleException("FINALIZED payroll records are immutable (PRD rule 12).");
    }

    private static PayrollItem BuildItem(PayrollRun run, Employee emp) => new()
    {
        TenantId = run.TenantId, PayrollRunId = run.PayrollRunId, EmployeeId = emp.EmployeeId,
        BasicSalary = emp.BasicSalary
    };

    private static void Compute(PayrollItem i)
    {
        i.GrossSalary = Money.Round(i.BasicSalary + i.Allowances + i.OvertimeAmount + i.BonusAmount);
        i.NetSalary = Money.Round(i.GrossSalary - i.Deductions - i.AdvanceDeduction -
                                  i.LoanDeduction - i.AbsenceDeduction);
    }

    private static void RecalculateTotals(PayrollRun run)
    {
        foreach (var item in run.Items) Compute(item);
        run.TotalGross = Money.Round(run.Items.Sum(i => i.GrossSalary));
        run.TotalDeductions = Money.Round(run.Items.Sum(i =>
            i.Deductions + i.AdvanceDeduction + i.LoanDeduction + i.AbsenceDeduction));
        run.TotalNet = Money.Round(run.Items.Sum(i => i.NetSalary));
    }

    private async Task<(PayrollRun, List<PayrollItemDto>)> FindFullAsync(long id, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.PayrollRunId == id && r.TenantId ==
                currentUser.RequireTenantId(), ct)
            ?? throw new NotFoundException("Payroll run not found.");
        var items = await (from i in db.PayrollItems.AsNoTracking()
                           join e in db.Employees.AsNoTracking() on i.EmployeeId equals e.EmployeeId
                           where i.PayrollRunId == id
                           orderby e.EmployeeCode
                           select new { i, e }).ToListAsync(ct);
        var dtos = items.Select(x => new PayrollItemDto(
            x.i.PayrollItemId, x.e.EmployeeId, $"{x.e.FirstName} {x.e.LastName}".Trim(),
            x.e.EmployeeCode, x.e.Department!.Name, x.i.BasicSalary, x.i.Allowances, x.i.OvertimeAmount,
            x.i.BonusAmount, x.i.Deductions, x.i.AdvanceDeduction, x.i.LoanDeduction, x.i.AbsenceDeduction,
            x.i.GrossSalary, x.i.NetSalary, x.i.Remarks)).ToList();
        var runDto = new PayrollRunDto(run.PayrollRunId, run.PayrollMonth, run.PeriodStart, run.PeriodEnd,
            run.Status, run.TotalGross, run.TotalDeductions, run.TotalNet, run.FinalizedAt, run.PaidAt,
            run.Notes, dtos);
        return (run, dtos);
    }
}



