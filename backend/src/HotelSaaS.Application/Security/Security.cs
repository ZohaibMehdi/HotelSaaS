using System.Security.Cryptography;

namespace HotelSaaS.Application.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing (.NET built-in; no external dependency).
/// Stored format: PBKDF2-SHA256.&lt;iterations&gt;.&lt;saltBase64&gt;.&lt;keyBase64&gt;
/// Lives in Application so seeding and services share one implementation.
/// </summary>
public static class PasswordHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const string Marker = "PBKDF2-SHA256";

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        var key = pbkdf2.GetBytes(KeySize);
        return $"{Marker}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash)) return false;
        var parts = storedHash.Split('.');
        if (parts.Length != 4 || parts[0] != Marker) return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations < 1) return false;

        byte[] salt, expectedKey;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedKey = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var actualKey = pbkdf2.GetBytes(expectedKey.Length);
        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }

    public static string Sha256Hex(string input) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input)));
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(Domain.Entities.User user);
    (string RawToken, string HashedToken, DateTime ExpiresAtUtc) CreateRefreshToken();

    static string HashRefreshToken(string rawToken) => PasswordHasher.Sha256Hex(rawToken);
}
