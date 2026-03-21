namespace TaskFlow.Application.DTOs.Users;

public sealed record UpdateProfileRequest(string? Name, string? Email, string? CurrentPassword, string? NewPassword, string? AvatarUrl);

public sealed record UserSummaryResponse(Guid Id, string Name, string Email, string? AvatarUrl);

public sealed record UserProfileResponse(Guid Id, string Name, string Email, string? AvatarUrl, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
