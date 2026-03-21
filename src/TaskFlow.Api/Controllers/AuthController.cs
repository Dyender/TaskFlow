using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.DTOs.Auth;

namespace TaskFlow.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ApiControllerBase
{
    [HttpPost("register")]
    public ActionResult<AuthResponse> Register([FromBody] RegisterRequest request) =>
        Ok(authService.Register(request));

    [HttpPost("login")]
    public ActionResult<AuthResponse> Login([FromBody] LoginRequest request) =>
        Ok(authService.Login(request));

    [HttpPost("refresh")]
    public ActionResult<AuthResponse> Refresh([FromBody] RefreshRequest request) =>
        Ok(authService.Refresh(request));
}
