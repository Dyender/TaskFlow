using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Entities;

public sealed class User : Entity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}

public sealed class RefreshSession : Entity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    public bool IsActive(DateTime utcNow) => RevokedAtUtc is null && ExpiresAtUtc > utcNow;
}
