using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Contracts;
using TaskFlow.Application.DTOs.Cards;

namespace TaskFlow.Api.Controllers;

[Authorize]
[Route("api")]
public sealed class CardsController(ICardService cardService) : ApiControllerBase
{
    [HttpPost("columns/{columnId:guid}/cards")]
    public ActionResult<CardDetailsResponse> Create(Guid columnId, [FromBody] CreateCardRequest request) =>
        Ok(cardService.Create(CurrentUserId, columnId, request));

    [HttpGet("cards/{id:guid}")]
    public ActionResult<CardDetailsResponse> GetById(Guid id) =>
        Ok(cardService.GetById(CurrentUserId, id));

    [HttpPut("cards/{id:guid}")]
    public ActionResult<CardDetailsResponse> Update(Guid id, [FromBody] UpdateCardRequest request) =>
        Ok(cardService.Update(CurrentUserId, id, request));

    [HttpDelete("cards/{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        cardService.Delete(CurrentUserId, id);
        return NoContent();
    }

    [HttpPut("cards/{id:guid}/move")]
    public ActionResult<CardDetailsResponse> Move(Guid id, [FromBody] MoveCardRequest request) =>
        Ok(cardService.Move(CurrentUserId, id, request));

    [HttpPut("cards/{id:guid}/assign")]
    public ActionResult<CardDetailsResponse> Assign(Guid id, [FromBody] AssignCardRequest request) =>
        Ok(cardService.Assign(CurrentUserId, id, request));

    [HttpPut("cards/{id:guid}/labels")]
    public ActionResult<CardDetailsResponse> UpdateLabels(Guid id, [FromBody] UpdateCardLabelsRequest request) =>
        Ok(cardService.UpdateLabels(CurrentUserId, id, request));

    [HttpPost("cards/{cardId:guid}/checklist")]
    public ActionResult<ChecklistItemResponse> AddChecklistItem(Guid cardId, [FromBody] AddChecklistItemRequest request) =>
        Ok(cardService.AddChecklistItem(CurrentUserId, cardId, request));

    [HttpPut("checklist-items/{id:guid}")]
    public ActionResult<ChecklistItemResponse> UpdateChecklistItem(Guid id, [FromBody] UpdateChecklistItemRequest request) =>
        Ok(cardService.UpdateChecklistItem(CurrentUserId, id, request));

    [HttpDelete("checklist-items/{id:guid}")]
    public IActionResult DeleteChecklistItem(Guid id)
    {
        cardService.DeleteChecklistItem(CurrentUserId, id);
        return NoContent();
    }
}
