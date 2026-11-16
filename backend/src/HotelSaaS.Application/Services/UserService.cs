using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Auth;
using HotelSaaS.Application.Dtos.Users;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Application.Security;
using HotelSaaS.Domain;
using HotelSaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Application.Services;

public interface IUserService
{
    Task<PagedResult<UserSummaryDto>> SearchAsync(long? tenantId, int page, int pageSize, string? search,
        CancellationToken ct = default);
    Task<UserSummaryDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserSummaryDto> UpdateAsync(long userId, UpdateUserRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(long userId, AdminResetPasswordRequest request, CancellationToken ct = default);
    Task SetActiveAsync(long userId, bool isActive, CancellationToken ct = default);
}

public class UserService(
    IAppDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IUserService
{
    public async Task<PagedResult<UserSummaryDto>> SearchAsync(long? tenantId, int page, int pageSize,
        string? search, CancellationToken ct = default)
    {
        var scope = TenantScope.Optional(currentUser, tenantId);

        var q = db.Users.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.Tenant)
            .AsQueryable();
        if (scope.HasValue) q = q.Where(u => u.TenantId == scope.Value);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(u => u.FullName.Contains(search) || u.Email.Contains(search));
        q = q.OrderByDescending(u => u.CreatedAt);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<UserSummaryDto>(items.Select(ToSummary).ToList(), page, pageSize, total);
    }

    public async Task<UserSummaryDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        Validate.Input(new CreateUserValidator(), request);

        long? targetTenant;
        if (request.Role == RoleNames.SuperAdmin)
        {
            if (!currentUser.IsSuperAdmin)
                throw new ForbiddenException("Only a SuperAdmin can create platform users.");
            targetTenant = request.TenantId; // null => platform user
        }
        else
        {
            // Tenant users: tenant always comes from the caller's identity (rule 5).
            targetTenant = TenantScope.Resolve(currentUser, request.TenantId != currentUser.TenantId
                ? (currentUser.IsSuperAdmin ? request.TenantId : null)
                : currentUser.TenantId);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var emailTaken = await db.Users.AnyAsync(u =>
            u.Email == email && (u.TenantId == targetTenant ||
                                 (targetTenant == null && u.TenantId == null)), ct);
        if (emailTaken)
            throw new ConflictException($"A user with email '{email}' already exists in this scope.");

        if (targetTenant.HasValue && !await db.Tenants.AnyAsync(t => t.TenantId == targetTenant.Value, ct))
            throw new NotFoundException("Target tenant not found.");

        var roleId = await RoleIdFor(request.Role, ct);
        var user = new User
        {
            TenantId = targetTenant,
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = request.Phone,
            PasswordHash = PasswordHasher.Hash(request.Password),
            MustChangePassword = true, // creator-supplied temporary password
            CreatedBy = currentUser.UserId
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = roleId });
        await db.SaveChangesAsync(ct);

        await audit.LogAsync("Create", nameof(User), user.UserId, null,
            new { user.Email, Role = request.Role }, user.TenantId, ct);

        return (await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Include(u => u.Tenant)
            .FirstAsync(u => u.UserId == user.UserId, ct)).Pipe(ToSummary);
    }

    public async Task<UserSummaryDto> UpdateAsync(long userId, UpdateUserRequest request, CancellationToken ct = default)
    {
        Validate.Input(new UpdateUserValidator(), request);
        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct)
            ?? throw new NotFoundException();
        EnsureCanManage(user);

        var old = new { user.FullName, Roles = string.Join(",", user.UserRoles.Select(r => r.Role!.Name)) };
        user.FullName = request.FullName.Trim();
        user.Phone = request.Phone;
        user.IsActive = request.IsActive;
        user.UpdatedBy = currentUser.UserId;

        if (!string.IsNullOrEmpty(request.Role))
        {
            var roleId = await RoleIdFor(request.Role, ct);
            if (user.UserRoles.All(r => r.RoleId != roleId))
            {
                db.UserRoles.RemoveRange(user.UserRoles);
                user.UserRoles.Clear();
                db.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
                await db.SaveChangesAsync(ct);
                user = await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Include(u => u.Tenant)
                    .FirstAsync(u => u.UserId == userId, ct);
            }
        }
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("Update", nameof(User), userId, old, request, user.TenantId, ct);
        return ToSummary(user);
    }

    public async Task ResetPasswordAsync(long userId, AdminResetPasswordRequest request, CancellationToken ct = default)
    {
        Validate.Input(new AdminResetPasswordValidator(), request);
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct) ?? throw new NotFoundException();
        EnsureCanManage(user);
        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.MustChangePassword = true;
        user.UpdatedBy = currentUser.UserId;
        var sessions = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        sessions.ForEach(t => t.RevokedAt = DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("ResetPassword", nameof(User), userId, null, null, user.TenantId, ct);
    }

    public async Task SetActiveAsync(long userId, bool isActive, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct) ?? throw new NotFoundException();
        EnsureCanManage(user);
        user.IsActive = isActive;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = currentUser.UserId;
        if (!isActive)
        {
            var sessions = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
            sessions.ForEach(t => t.RevokedAt = DateTime.UtcNow);
        }
        await db.SaveChangesAsync(ct);
        await audit.LogAsync(isActive ? "Activate" : "Deactivate", nameof(User), userId,
            null, new { isActive }, user.TenantId, ct);
    }

    private void EnsureCanManage(User target)
    {
        if (currentUser.IsSuperAdmin) return;
        if (currentUser.TenantId != target.TenantId || target.TenantId == null)
            throw new ForbiddenException();
    }

    private async Task<byte> RoleIdFor(string roleName, CancellationToken ct)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct)
                   ?? throw new BadRequestException($"Unknown role '{roleName}'.");
        return role.RoleId;
    }

    internal static UserSummaryDto ToSummary(User user) => new(
        user.UserId, user.TenantId, user.Tenant?.BusinessName, user.FullName, user.Email, user.Phone,
        user.UserRoles.Select(r => r.Role!.Name).ToList(), user.IsActive, user.MustChangePassword, user.LastLoginAt);
}

internal static class PipeExtensions
{
    public static TOut Pipe<TIn, TOut>(this TIn input, Func<TIn, TOut> map) => map(input);
}
