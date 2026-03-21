using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using TaskFlow.Application.Contracts;

namespace TaskFlow.Api.Authentication;

public sealed class TaskFlowAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ITokenService tokenService,
    ITaskFlowRepository repository) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var authorizationHeader = headerValues.ToString();
        if (!authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();
        if (!tokenService.TryValidateAccessToken(token, out var payload) || payload is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid or expired access token."));
        }

        var user = repository.GetUserById(payload.UserId);
        if (user is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("The user associated with this token no longer exists."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
