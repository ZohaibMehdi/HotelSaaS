namespace HotelSaaS.Application.Common;

/// <summary>
/// Server-side identity context resolved from JWT claims. Client-provided
/// tenant identifiers are NEVER used for authorization (PRD rules 4-5).
/// </summary>
public interface ICurrentUser
{
    long? UserId { get; }
    long? TenantId { get; }
    bool IsSuperAdmin { get; }
    IReadOnlyList<string> Roles { get; }
    string? UserName { get; }
    string? IpAddress { get; }
    string? CorrelationId { get; }

    /// <summary>Tenant id required for tenant-scoped operations; throws if absent.</summary>
    long RequireTenantId();
}

public interface ICurrentUserService : ICurrentUser { }
