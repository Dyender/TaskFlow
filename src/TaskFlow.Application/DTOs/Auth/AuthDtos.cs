namespace TaskFlow.Application.DTOs.Auth;

public sealed record RegisterRequest(string Name, string Email, string Password, string? AvatarUrl);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record AuthResponse(
    Guid UserId,
    string Name,
    string Email,
    string? AvatarUrl,
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc,
    DateTime RefreshTokenExpiresAtUtc);
