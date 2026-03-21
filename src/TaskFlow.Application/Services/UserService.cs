using TaskFlow.Application.Contracts;
using TaskFlow.Application.DTOs.Users;

namespace TaskFlow.Application.Services;

public sealed class UserService(
    ITaskFlowRepository repository,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider) : IUserService
{
    public UserProfileResponse GetProfile(Guid userId)
    {
        var user = repository.GetUserById(userId)
            ?? throw new NotFoundException("User not found.");

        return user.ToProfileResponse();
    }

    public UserProfileResponse UpdateProfile(Guid userId, UpdateProfileRequest request)
    {
        var user = repository.GetUserById(userId)
            ?? throw new NotFoundException("User not found.");

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            user.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var existing = repository.GetUserByEmail(normalizedEmail);
            if (existing is not null && existing.Id != userId)
            {
                throw new ConflictException("This email is already in use.");
            }

            user.Email = normalizedEmail;
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            {
                throw new ValidationAppException("Current password is required to change password.");
            }

            if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            {
                throw new ValidationAppException("Current password is incorrect.");
            }

            if (request.NewPassword.Length < 8)
            {
                throw new ValidationAppException("New password must be at least 8 characters long.");
            }

            user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        }

        if (request.AvatarUrl is not null)
        {
            user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        }

        user.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        repository.UpdateUser(user);
        repository.SaveChanges();

        return user.ToProfileResponse();
    }
}
