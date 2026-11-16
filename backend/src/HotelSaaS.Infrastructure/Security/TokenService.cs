using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HotelSaaS.Application.Security;
using HotelSaaS.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace HotelSaaS.Infrastructure.Security;

public class JwtSettings
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "HotelSaaS.Api";
    public string Audience { get; set; } = "HotelSaaS.Clients";
    /// <summary>Minimum 32 characters. MUST be overridden via environment/secret store in production.</summary>
    public string SecretKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 480;
    public int RefreshTokenDays { get; set; } = 7;
}

public class TokenService(IOptions<JwtSettings> options) : ITokenService
{
    private readonly JwtSettings _settings = options.Value;

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user)
    {
        var expires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new("tenantId", user.TenantId?.ToString() ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        claims.AddRange(user.UserRoles.Select(ur => new Claim(ClaimTypes.Role, ur.Role!.Name)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_settings.Issuer, _settings.Audience, claims,
            notBefore: DateTime.UtcNow, expires: expires, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public (string RawToken, string HashedToken, DateTime ExpiresAtUtc) CreateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        return (raw, ITokenService.HashRefreshToken(raw), DateTime.UtcNow.AddDays(_settings.RefreshTokenDays));
    }
}
