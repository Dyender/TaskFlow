using Microsoft.AspNetCore.Mvc;
using TaskFlow.Api.Extensions;

namespace TaskFlow.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid CurrentUserId => User.GetRequiredUserId();
}
