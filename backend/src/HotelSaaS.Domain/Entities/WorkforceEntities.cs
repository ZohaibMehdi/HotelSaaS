using HotelSaaS.Domain;

namespace HotelSaaS.Domain.Entities;

public class Department
{
    public long DepartmentId { get; set; }
    public long TenantId { get; set; }
    public string Name { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Employee> Employees { get; set; } = [];
}

public class Employee
{
    public long EmployeeId { get; set; }
    public long TenantId { get; set; }
    public string EmployeeCode { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string? LastName { get; set; }
    public string? Cnic { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public long? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public string? Designation { get; set; }
    public DateOnly JoiningDate { get; set; }
    public EmploymentStatus EmploymentStatus { get; set; } = EmploymentStatus.Active;
    public decimal BasicSalary { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PayrollRun
{
    public long PayrollRunId { get; set; }
    public long TenantId { get; set; }
    public DateOnly PayrollMonth { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;
    public decimal TotalGross { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalNet { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public long? FinalizedBy { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<PayrollItem> Items { get; set; } = [];
}

public class PayrollItem
{
    public long PayrollItemId { get; set; }
    public long PayrollRunId { get; set; }
    public PayrollRun PayrollRun { get; set; } = default!;
    public long TenantId { get; set; }
    public long EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;
    public decimal BasicSalary { get; set; }
    public decimal Allowances { get; set; }
    public decimal OvertimeAmount { get; set; }
    public decimal BonusAmount { get; set; }
    public decimal Deductions { get; set; }
    public decimal AdvanceDeduction { get; set; }
    public decimal LoanDeduction { get; set; }
    public decimal AbsenceDeduction { get; set; }
    public decimal GrossSalary { get; set; }
    public decimal NetSalary { get; set; }
    public string? Remarks { get; set; }
}
