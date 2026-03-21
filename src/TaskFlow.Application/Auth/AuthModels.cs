namespace TaskFlow.Application.Auth;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "TaskFlow";
    public string Audience { get; init; } = "TaskFlow.Client";
    public string SecretKey { get; init; } = "ChangeThisSecretKeyForProductionTaskFlow123!";
    public int AccessTokenMinutes { get; init; } = 30;
    public int RefreshTokenDays { get; init; } = 14;
}

public sealed record TokenPayload(Guid UserId, string Email, string Name, DateTime ExpiresAtUtc);

public sealed record AuthTokens(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAtUtc, DateTime RefreshTokenExpiresAtUtc);
