using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.DTOs.Columns;

namespace TaskFlow.Api.Controllers;

[Authorize]
[Route("api")]
public sealed class ColumnsController(IBoardService boardService) : ApiControllerBase
{
    [HttpPost("boards/{boardId:guid}/columns")]
    public ActionResult<ColumnResponse> Create(Guid boardId, [FromBody] CreateColumnRequest request) =>
        Ok(boardService.CreateColumn(CurrentUserId, boardId, request));

    [HttpPut("columns/{id:guid}")]
    public ActionResult<ColumnResponse> Update(Guid id, [FromBody] UpdateColumnRequest request) =>
        Ok(boardService.UpdateColumn(CurrentUserId, id, request));

    [HttpDelete("columns/{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        boardService.DeleteColumn(CurrentUserId, id);
        return NoContent();
    }

    [HttpPut("columns/reorder")]
    public ActionResult<IReadOnlyCollection<ColumnResponse>> Reorder([FromBody] ReorderColumnsRequest request) =>
        Ok(boardService.ReorderColumns(CurrentUserId, request));
}
