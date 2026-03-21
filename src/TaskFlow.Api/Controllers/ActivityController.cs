using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.DTOs.Activity;

namespace TaskFlow.Api.Controllers;

[Authorize]
[Route("api/cards")]
public sealed class ActivityController(IActivityService activityService) : ApiControllerBase
{
    [HttpGet("{id:guid}/activity")]
    public ActionResult<IReadOnlyCollection<ActivityResponse>> GetActivity(Guid id) =>
        Ok(activityService.GetCardActivity(CurrentUserId, id));
}
