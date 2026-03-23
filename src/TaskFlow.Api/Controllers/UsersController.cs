using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.DTOs.Users;

namespace TaskFlow.Api.Controllers;

[Authorize]
[Route("api/users")]
public sealed class UsersController(IUserService userService) : ApiControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyCollection<UserSummaryResponse>> Search([FromQuery] string? search) =>
        Ok(userService.Search(CurrentUserId, search));

    [HttpGet("me")]
    public ActionResult<UserProfileResponse> GetMe() =>
        Ok(userService.GetProfile(CurrentUserId));

    [HttpPut("me")]
    public ActionResult<UserProfileResponse> UpdateMe([FromBody] UpdateProfileRequest request) =>
        Ok(userService.UpdateProfile(CurrentUserId, request));
}
