using TaskFlow.Application.Auth;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.DTOs.Auth;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Services;

public sealed class AuthService(
    ITaskFlowRepository repository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    TimeProvider timeProvider) : IAuthService
{
    public AuthResponse Register(RegisterRequest request)
    {
        ValidateRegisterRequest(request);
        var normalizedEmail = NormalizeEmail(request.Email);

        if (repository.GetUserByEmail(normalizedEmail) is not null)
        {
            throw new ConflictException("A user with this email already exists.");
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var user = new User
        {
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            PasswordHash = passwordHasher.Hash(request.Password),
            AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim(),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        repository.AddUser(user);
        var tokens = tokenService.CreateTokens(user, utcNow);
        repository.AddRefreshSession(new RefreshSession
        {
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(tokens.RefreshToken),
            ExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        });

        repository.SaveChanges();
        return ToAuthResponse(user, tokens);
    }

    public AuthResponse Login(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationAppException("Email and password are required.");
        }

        var normalizedEmail = NormalizeEmail(request.Email);
        var user = repository.GetUserByEmail(normalizedEmail)
            ?? throw new ValidationAppException("Invalid email or password.");

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new ValidationAppException("Invalid email or password.");
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var tokens = tokenService.CreateTokens(user, utcNow);
        repository.AddRefreshSession(new RefreshSession
        {
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(tokens.RefreshToken),
            ExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        });

        repository.SaveChanges();
        return ToAuthResponse(user, tokens);
    }

    public AuthResponse Refresh(RefreshRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new ValidationAppException("Refresh token is required.");
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var session = repository.GetRefreshSessionByTokenHash(tokenHash)
            ?? throw new ValidationAppException("Refresh token is invalid.");

        if (!session.IsActive(utcNow))
        {
            throw new ValidationAppException("Refresh token has expired or been revoked.");
        }

        var user = repository.GetUserById(session.UserId)
            ?? throw new NotFoundException("User associated with the refresh token was not found.");

        session.RevokedAtUtc = utcNow;
        session.UpdatedAtUtc = utcNow;
        repository.UpdateRefreshSession(session);

        var tokens = tokenService.CreateTokens(user, utcNow);
        repository.AddRefreshSession(new RefreshSession
        {
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(tokens.RefreshToken),
            ExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        });

        repository.SaveChanges();
        return ToAuthResponse(user, tokens);
    }

    private static void ValidateRegisterRequest(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationAppException("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ValidationAppException("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new ValidationAppException("Password must be at least 8 characters long.");
        }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static AuthResponse ToAuthResponse(User user, AuthTokens tokens) =>
        new(
            user.Id,
            user.Name,
            user.Email,
            user.AvatarUrl,
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.AccessTokenExpiresAtUtc,
            tokens.RefreshTokenExpiresAtUtc);
}
