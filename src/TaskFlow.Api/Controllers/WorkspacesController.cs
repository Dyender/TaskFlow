using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.DTOs.Labels;
using TaskFlow.Application.DTOs.Workspaces;

namespace TaskFlow.Api.Controllers;

[Authorize]
[Route("api/workspaces")]
public sealed class WorkspacesController(IWorkspaceService workspaceService) : ApiControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyCollection<WorkspaceResponse>> GetMine() =>
        Ok(workspaceService.GetForUser(CurrentUserId));

    [HttpPost]
    public ActionResult<WorkspaceDetailsResponse> Create([FromBody] CreateWorkspaceRequest request)
    {
        var workspace = workspaceService.Create(CurrentUserId, request);
        return CreatedAtAction(nameof(GetById), new { id = workspace.Id }, workspace);
    }

    [HttpGet("{id:guid}")]
    public ActionResult<WorkspaceDetailsResponse> GetById(Guid id) =>
        Ok(workspaceService.GetById(CurrentUserId, id));

    [HttpPost("{id:guid}/members")]
    public ActionResult<WorkspaceDetailsResponse> AddMember(Guid id, [FromBody] AddWorkspaceMemberRequest request) =>
        Ok(workspaceService.AddMember(CurrentUserId, id, request));

    [HttpGet("{id:guid}/labels")]
    public ActionResult<IReadOnlyCollection<LabelResponse>> GetLabels(Guid id) =>
        Ok(workspaceService.GetLabels(CurrentUserId, id));

    [HttpPost("{id:guid}/labels")]
    public ActionResult<LabelResponse> CreateLabel(Guid id, [FromBody] CreateLabelRequest request) =>
        Ok(workspaceService.CreateLabel(CurrentUserId, id, request));
}
