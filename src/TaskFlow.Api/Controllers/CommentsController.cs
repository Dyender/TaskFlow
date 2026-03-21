using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.DTOs.Comments;

namespace TaskFlow.Api.Controllers;

[Authorize]
[Route("api")]
public sealed class CommentsController(ICardService cardService) : ApiControllerBase
{
    [HttpPost("cards/{cardId:guid}/comments")]
    public ActionResult<CommentResponse> Add(Guid cardId, [FromBody] CreateCommentRequest request) =>
        Ok(cardService.AddComment(CurrentUserId, cardId, request));

    [HttpPut("comments/{id:guid}")]
    public ActionResult<CommentResponse> Update(Guid id, [FromBody] UpdateCommentRequest request) =>
        Ok(cardService.UpdateComment(CurrentUserId, id, request));

    [HttpDelete("comments/{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        cardService.DeleteComment(CurrentUserId, id);
        return NoContent();
    }
}
