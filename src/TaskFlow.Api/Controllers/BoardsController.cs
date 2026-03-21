using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.DTOs.Boards;
using TaskFlow.Application.DTOs.Cards;

namespace TaskFlow.Api.Controllers;

[Authorize]
[Route("api/boards")]
public sealed class BoardsController(IBoardService boardService) : ApiControllerBase
{
    [HttpGet("{id:guid}")]
    public ActionResult<BoardDetailsResponse> GetById(Guid id) =>
        Ok(boardService.GetById(CurrentUserId, id));

    [HttpPost("/api/workspaces/{workspaceId:guid}/boards")]
    public ActionResult<BoardDetailsResponse> Create(Guid workspaceId, [FromBody] CreateBoardRequest request)
    {
        var board = boardService.Create(CurrentUserId, workspaceId, request);
        return CreatedAtAction(nameof(GetById), new { id = board.Id }, board);
    }

    [HttpPut("{id:guid}")]
    public ActionResult<BoardDetailsResponse> Update(Guid id, [FromBody] UpdateBoardRequest request) =>
        Ok(boardService.Update(CurrentUserId, id, request));

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        boardService.Archive(CurrentUserId, id);
        return NoContent();
    }

    [HttpPost("{id:guid}/members")]
    public ActionResult<BoardDetailsResponse> AddMember(Guid id, [FromBody] AddBoardMemberRequest request) =>
        Ok(boardService.AddMember(CurrentUserId, id, request));

    [HttpGet("{id:guid}/cards")]
    public ActionResult<IReadOnlyCollection<CardResponse>> SearchCards(
        Guid id,
        [FromQuery] string? search,
        [FromQuery] Guid? assigneeId,
        [FromQuery] Domain.Enums.TaskPriority? priority,
        [FromQuery] Guid[]? labelIds,
        [FromQuery] DateTime? dueBeforeUtc,
        [FromQuery] bool onlyOverdue = false,
        [FromQuery] bool includeArchived = false) =>
        Ok(boardService.SearchBoardCards(CurrentUserId, id, new BoardCardsQuery(search, assigneeId, priority, labelIds, dueBeforeUtc, onlyOverdue, includeArchived)));
}
