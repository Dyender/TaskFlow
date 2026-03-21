using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TaskFlow.Application.Auth;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Security;

public sealed class JwtTokenService(JwtSettings settings) : ITokenService
{
    private readonly byte[] _secretKey = Encoding.UTF8.GetBytes(settings.SecretKey);

    public AuthTokens CreateTokens(User user, DateTime utcNow)
    {
        var accessTokenExpiresAtUtc = utcNow.AddMinutes(settings.AccessTokenMinutes);
        var refreshTokenExpiresAtUtc = utcNow.AddDays(settings.RefreshTokenDays);

        var header = new Dictionary<string, object?>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT"
        };

        var payload = new Dictionary<string, object?>
        {
            ["sub"] = user.Id.ToString(),
            ["email"] = user.Email,
            ["name"] = user.Name,
            ["iss"] = settings.Issuer,
            ["aud"] = settings.Audience,
            ["iat"] = ToUnixTimeSeconds(utcNow),
            ["exp"] = ToUnixTimeSeconds(accessTokenExpiresAtUtc)
        };

        var accessToken = $"{Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header))}.{Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload))}";
        var signature = ComputeSignature(accessToken);
        var refreshToken = Base64UrlEncode(RandomNumberGenerator.GetBytes(48));

        return new AuthTokens($"{accessToken}.{signature}", refreshToken, accessTokenExpiresAtUtc, refreshTokenExpiresAtUtc);
    }

    public string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(bytes);
    }

    public bool TryValidateAccessToken(string accessToken, out TokenPayload? payload)
    {
        payload = null;
        var parts = accessToken.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        var unsignedToken = $"{parts[0]}.{parts[1]}";
        var expectedSignature = ComputeSignature(unsignedToken);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expectedSignature), Encoding.ASCII.GetBytes(parts[2])))
        {
            return false;
        }

        try
        {
            var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Base64UrlDecode(parts[1]));
            if (json is null)
            {
                return false;
            }

            var issuer = json["iss"].GetString();
            var audience = json["aud"].GetString();
            if (!string.Equals(issuer, settings.Issuer, StringComparison.Ordinal) ||
                !string.Equals(audience, settings.Audience, StringComparison.Ordinal))
            {
                return false;
            }

            var expiration = FromUnixTimeSeconds(json["exp"].GetInt64());
            if (expiration <= DateTime.UtcNow)
            {
                return false;
            }

            payload = new TokenPayload(
                Guid.Parse(json["sub"].GetString() ?? string.Empty),
                json["email"].GetString() ?? string.Empty,
                json["name"].GetString() ?? string.Empty,
                expiration);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private string ComputeSignature(string value)
    {
        using var hmac = new HMACSHA256(_secretKey);
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Base64UrlEncode(signatureBytes);
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var normalized = input.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
        return Convert.FromBase64String(normalized);
    }

    private static long ToUnixTimeSeconds(DateTime utcDateTime) =>
        new DateTimeOffset(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc)).ToUnixTimeSeconds();

    private static DateTime FromUnixTimeSeconds(long value) =>
        DateTimeOffset.FromUnixTimeSeconds(value).UtcDateTime;
}
