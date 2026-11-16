namespace HotelSaaS.Domain.Entities;

public class Tenant
{
    public long TenantId { get; set; }
    public string TenantCode { get; set; } = default!;
    public string BusinessName { get; set; } = default!;
    public string? LegalName { get; set; }
    public TenantStatus Status { get; set; }
    public string ContactEmail { get; set; } = default!;
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public string Currency { get; set; } = "PKR";
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<User> Users { get; set; } = [];
    public CompanyInformation? CompanyInformation { get; set; }
    public PropertySettings? PropertySettings { get; set; }
}

public class Role
{
    public byte RoleId { get; set; }
    public string Name { get; set; } = default!;
    public ICollection<UserRole> UserRoles { get; set; } = [];
}

public class User
{
    public long UserId { get; set; }
    public long? TenantId { get; set; }
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }
    public bool MustChangePassword { get; set; }
    public string? ResetTokenHash { get; set; }
    public DateTime? ResetTokenExpiresAt { get; set; }
    public short FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant? Tenant { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public IEnumerable<string> RoleNames => UserRoles.Select(ur => ur.Role!.Name);
}

public class UserRole
{
    public long UserId { get; set; }
    public User User { get; set; } = default!;
    public byte RoleId { get; set; }
    public Role? Role { get; set; }
}

public class RefreshToken
{
    public long TokenId { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = default!;
    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public long? ReplacedByToken { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SystemSetting
{
    public int SettingId { get; set; }
    public string SettingKey { get; set; } = default!;
    public string? SettingValue { get; set; }
    public string? Description { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }
}
