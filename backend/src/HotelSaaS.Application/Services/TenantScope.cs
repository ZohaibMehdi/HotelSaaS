using HotelSaaS.Application.Common;

namespace HotelSaaS.Application.Services;

/// <summary>
/// Tenant scope resolution helper (PRD rules 4-5).
/// Tenant users are ALWAYS bound to their claim tenant. Only SuperAdmin may
/// pass an explicit tenantId for platform-access scenarios.
/// </summary>
public static class TenantScope
{
    /// <summary>For tenant-owned operations. SuperAdmin must supply an explicit tenant.</summary>
    public static long Resolve(ICurrentUser user, long? requestedTenantId)
    {
        if (user.IsSuperAdmin)
        {
            return requestedTenantId.HasValue
                ? requestedTenantId.Value
                : throw new BadRequestException("A tenantId parameter is required for this operation.");
        }
        return user.RequireTenantId();
    }

    /// <summary>For read endpoints where SuperAdmin may omit tenantId to view across tenants.</summary>
    public static long? Optional(ICurrentUser user, long? requestedTenantId)
    {
        if (user.IsSuperAdmin) return requestedTenantId;
        return user.RequireTenantId();
    }
}
