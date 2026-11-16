using Microsoft.Extensions.Hosting;
using HotelSaaS.Application.Common;
using HotelSaaS.Application.Dtos.Auth;
using HotelSaaS.Application.Interfaces;
using HotelSaaS.Application.Security;
using Microsoft.EntityFrameworkCore;

namespace HotelSaaS.Application.Services;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<LoginResponse> RefreshAsync(string rawRefreshToken, CancellationToken ct = default);
    Task ChangePasswordAsync(long userId, ChangePasswordRequest request, CancellationToken ct = default);
    Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default);
    Task<UserSummaryDto> GetProfileAsync(long userId, CancellationToken ct = default);
    Task LogoutAsync(string? rawRefreshToken, CancellationToken ct = default);
}

public class AuthService(
    IAppDbContext db,
    ITokenService tokens,
    INotificationService notifications,
    ICurrentUser currentUser,
    IHostEnvironment env,
    ILogger<AuthService> logger) : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

        // Uniform failure response - never reveal whether the account exists.
        if (user is null || !user.IsActive)
            throw new UnauthorizedException("Invalid email or password.");
        if (user.TenantId.HasValue && (!user.Tenant!.IsActive || user.Tenant.Status != Domain.TenantStatus.Active))
            throw new UnauthorizedException("This account's organization is not active. Contact support.");

        if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
        {
            logger.LogWarning("Locked account login attempt: {Email}", email);
            throw new UnauthorizedException(
                $"Account temporarily locked until {user.LockedUntil:HH:mm} UTC due to repeated failures.");
        }

        if (!PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
            {
                user.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
                user.FailedLoginCount = 0;
                logger.LogWarning("Account {Email} locked after {Attempts} failures.", email, MaxFailedAttempts);
            }
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException("Invalid email or password.");
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;

        // Single active refresh session per user.
        var activeTokens = await db.RefreshTokens
            .Where(t => t.UserId == user.UserId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);
        activeTokens.ForEach(t => t.RevokedAt = DateTime.UtcNow);

        var access = tokens.CreateAccessToken(user);
        var refresh = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(new Domain.Entities.RefreshToken
        {
            UserId = user.UserId,
            TokenHash = refresh.HashedToken,
            ExpiresAt = refresh.ExpiresAtUtc,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return new LoginResponse(access.Token, refresh.RawToken, access.ExpiresAtUtc,
            ToSummary(user), user.MustChangePassword);
    }

    public async Task<LoginResponse> RefreshAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        var hash = ITokenService.HashRefreshToken(rawRefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || stored.RevokedAt != null || stored.ExpiresAt <= DateTime.UtcNow)
            throw new UnauthorizedException("Session expired. Please sign in again.");

        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.UserId == stored.UserId, ct)
            ?? throw new UnauthorizedException("Session expired. Please sign in again.");
        if (!user.IsActive || (user.TenantId.HasValue && user.Tenant!.Status != Domain.TenantStatus.Active))
            throw new UnauthorizedException("Account inactive.");

        // Rotation
        stored.RevokedAt = DateTime.UtcNow;
        var access = tokens.CreateAccessToken(user);
        var refresh = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(new Domain.Entities.RefreshToken
        {
            UserId = user.UserId, TokenHash = refresh.HashedToken,
            ExpiresAt = refresh.ExpiresAtUtc, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return new LoginResponse(access.Token, refresh.RawToken, access.ExpiresAtUtc,
            ToSummary(user), user.MustChangePassword);
    }

    public async Task ChangePasswordAsync(long userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        Validate.Input(null, request); // validator invoked via controller pipeline; keep null-safe here
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct)
                   ?? throw new NotFoundException();
        if (!PasswordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new BusinessRuleException("Current password is incorrect.");

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;
        var sessions = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        sessions.ForEach(t => t.RevokedAt = DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive, ct);
        string? devToken = null;
        if (user != null)
        {
            var raw = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) +
                      Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            user.ResetTokenHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));
            user.ResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(30);
            await db.SaveChangesAsync(ct);

            notifications.SendEmail(user.Email, "Password reset",
                "Use the provided reset token to set a new password. It expires in 30 minutes.");
            if (env.IsDevelopment())
                devToken = raw; // Never exposed outside Development (PRD section 44).
        }
        return new ForgotPasswordResponse(
            "If the email exists, password reset instructions have been sent.", devToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct = default)
    {
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(request.Token)));
        var user = await db.Users.FirstOrDefaultAsync(u =>
            u.ResetTokenHash == hash && u.ResetTokenExpiresAt > DateTime.UtcNow && u.IsActive, ct);
        if (user is null)
            throw new BusinessRuleException("Reset link is invalid or has expired.");

        user.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        user.ResetTokenHash = null;
        user.ResetTokenExpiresAt = null;
        user.MustChangePassword = false;
        var sessions = await db.RefreshTokens.Where(t => t.UserId == user.UserId && t.RevokedAt == null).ToListAsync(ct);
        sessions.ForEach(t => t.RevokedAt = DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserSummaryDto> GetProfileAsync(long userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct)
            ?? throw new NotFoundException();
        return ToSummary(user);
    }

    public async Task LogoutAsync(string? rawRefreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken)) return;
        var hash = ITokenService.HashRefreshToken(rawRefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored != null) stored.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static UserSummaryDto ToSummary(Domain.Entities.User user) => new(
        user.UserId, user.TenantId, user.Tenant?.BusinessName, user.FullName, user.Email, user.Phone,
        user.UserRoles.Select(r => r.Role!.Name).ToList(), user.IsActive, user.MustChangePassword, user.LastLoginAt);
}



