using HotelSaaS.Domain;
using System.Security.Claims;
using HotelSaaS.Application.Common;
using Microsoft.AspNetCore.Http;

namespace HotelSaaS.Infrastructure.Security;

/// <summary>
/// Resolves identity exclusively from authenticated JWT claims (PRD rule 5).
/// Never reads tenant identifiers from request bodies/query strings.
/// </summary>
public class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public long? UserId =>
        long.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public long? TenantId
    {
        get
        {
            var raw = Principal?.FindFirstValue("tenantId");
            return long.TryParse(raw, out var tid) && tid > 0 ? tid : null;
        }
    }

    public bool IsSuperAdmin => Principal?.IsInRole(RoleNames.SuperAdmin) == true;

    public IReadOnlyList<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public string? UserName => Principal?.Identity?.Name;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? CorrelationId => accessor.HttpContext?.Items["CorrelationId"]?.ToString();

    public long RequireTenantId() => TenantId
        ?? throw new ForbiddenException("This operation requires a tenant context.");
}

