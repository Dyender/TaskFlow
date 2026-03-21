using TaskFlow.Application.Contracts;
using TaskFlow.Application.DTOs.Cards;
using TaskFlow.Application.DTOs.Comments;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Services;

public sealed class CardService(
    ITaskFlowRepository repository,
    IBoardRealtimeNotifier realtimeNotifier,
    TimeProvider timeProvider) : ICardService
{
    public CardDetailsResponse Create(Guid userId, Guid columnId, CreateCardRequest request)
    {
        var column = repository.GetColumn(columnId)
            ?? throw new NotFoundException("Column not found.");
        var (workspace, board) = LoadBoardContext(column.BoardId);
        AccessGuard.EnsureBoardContributor(workspace, board, userId);

        ValidateCardPayload(request.Title, request.DeadlineUtc);
        EnsureAssigneeCanBeUsed(workspace, board, request.AssigneeId);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var cards = repository.GetCardsByColumn(columnId).OrderBy(card => card.Position).ToList();
        var insertPosition = request.Position.HasValue
            ? Math.Clamp(request.Position.Value, 0, cards.Count)
            : cards.Count;

        var card = new TaskCard
        {
            BoardId = board.Id,
            ColumnId = columnId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Priority = request.Priority,
            DeadlineUtc = request.DeadlineUtc,
            AssigneeId = request.AssigneeId,
            AuthorId = userId,
            Position = insertPosition,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        cards.Insert(insertPosition, card);
        ReindexCards(cards, utcNow);
        repository.AddCard(card);

        if (card.AssigneeId.HasValue && card.AssigneeId.Value != userId)
        {
            AddNotification(card.AssigneeId.Value, NotificationType.Assignment, $"You were assigned to card '{card.Title}'.", card.Id);
        }

        AddActivity(workspace.Id, board.Id, card.Id, userId, ActivityType.CardCreated, $"Created card '{card.Title}'.");
        repository.SaveChanges();
        PublishBoardEvent(board.Id, "card.created", new { boardId = board.Id, cardId = card.Id, columnId });

        return BuildCardDetails(card);
    }

    public CardDetailsResponse GetById(Guid userId, Guid cardId)
    {
        var (workspace, board, card) = LoadCardContext(cardId);
        AccessGuard.EnsureCanViewBoard(workspace, board, userId);
        return BuildCardDetails(card);
    }

    public CardDetailsResponse Update(Guid userId, Guid cardId, UpdateCardRequest request)
    {
        var (workspace, board, card) = LoadCardContext(cardId);
        AccessGuard.EnsureCanEditCard(workspace, board, card, userId);

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            card.Title = request.Title.Trim();
        }

        if (request.Description is not null)
        {
            card.Description = request.Description.Trim();
        }

        if (request.Priority.HasValue)
        {
            card.Priority = request.Priority.Value;
        }

        if (request.ClearDeadline)
        {
            card.DeadlineUtc = null;
        }
        else if (request.DeadlineUtc.HasValue)
        {
            if (request.DeadlineUtc.Value < card.CreatedAtUtc)
            {
                throw new ValidationAppException("Deadline cannot be earlier than the card creation date.");
            }

            card.DeadlineUtc = request.DeadlineUtc.Value;
        }

        if (request.IsArchived.HasValue)
        {
            card.IsArchived = request.IsArchived.Value;
        }

        card.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        repository.UpdateCard(card);
        AddActivity(workspace.Id, board.Id, card.Id, userId, ActivityType.CardUpdated, $"Updated card '{card.Title}'.");
        repository.SaveChanges();
        PublishBoardEvent(board.Id, "card.updated", new { boardId = board.Id, cardId = card.Id });

        return BuildCardDetails(card);
    }

    public void Delete(Guid userId, Guid cardId)
    {
        var (workspace, board, card) = LoadCardContext(cardId);
        AccessGuard.EnsureCanEditCard(workspace, board, card, userId);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        repository.DeleteCard(cardId);
        ReindexCards(repository.GetCardsByColumn(card.ColumnId).OrderBy(item => item.Position).ToList(), utcNow);
        repository.SaveChanges();
        PublishBoardEvent(board.Id, "card.deleted", new { boardId = board.Id, cardId });
    }

    public CardDetailsResponse Move(Guid userId, Guid cardId, MoveCardRequest request)
    {
        var (workspace, board, card) = LoadCardContext(cardId);
        AccessGuard.EnsureBoardContributor(workspace, board, userId);

        var targetColumn = repository.GetColumn(request.TargetColumnId)
            ?? throw new ValidationAppException("Target column does not exist.");
        if (targetColumn.BoardId != board.Id)
        {
            throw new ValidationAppException("Cannot move a card to a column from another board.");
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var sourceCards = repository.GetCardsByColumn(card.ColumnId)
            .Where(existing => existing.Id != card.Id)
            .OrderBy(existing => existing.Position)
            .ToList();
        ReindexCards(sourceCards, utcNow);

        var targetCards = repository.GetCardsByColumn(targetColumn.Id)
            .Where(existing => existing.Id != card.Id)
            .OrderBy(existing => existing.Position)
            .ToList();

        var targetPosition = Math.Clamp(request.TargetPosition, 0, targetCards.Count);
        card.ColumnId = targetColumn.Id;
        targetCards.Insert(targetPosition, card);
        ReindexCards(targetCards, utcNow);
        card.UpdatedAtUtc = utcNow;
        repository.UpdateCard(card);

        AddActivity(workspace.Id, board.Id, card.Id, userId, ActivityType.CardMoved, $"Moved card '{card.Title}' to column '{targetColumn.Title}'.");

        if (card.AssigneeId.HasValue && card.AssigneeId.Value != userId)
        {
            AddNotification(card.AssigneeId.Value, NotificationType.CardMoved, $"Card '{card.Title}' was moved to '{targetColumn.Title}'.", card.Id);
        }

        repository.SaveChanges();
        PublishBoardEvent(board.Id, "card.moved", new { boardId = board.Id, cardId = card.Id, columnId = targetColumn.Id, position = card.Position });
        return BuildCardDetails(card);
    }

    public CardDetailsResponse Assign(Guid userId, Guid cardId, AssignCardRequest request)
    {
        var (workspace, board, card) = LoadCardContext(cardId);
        AccessGuard.EnsureBoardContributor(workspace, board, userId);
        EnsureAssigneeCanBeUsed(workspace, board, request.AssigneeId);

        card.AssigneeId = request.AssigneeId;
        card.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        repository.UpdateCard(card);

        if (request.AssigneeId.HasValue && request.AssigneeId.Value != userId)
        {
            AddNotification(request.AssigneeId.Value, NotificationType.Assignment, $"You were assigned to card '{card.Title}'.", card.Id);
        }

        AddActivity(
            workspace.Id,
            board.Id,
            card.Id,
            userId,
            ActivityType.CardAssigned,
            request.AssigneeId.HasValue
                ? $"Assigned card '{card.Title}' to user '{request.AssigneeId}'."
                : $"Cleared assignee for card '{card.Title}'.");

        repository.SaveChanges();
        PublishBoardEvent(board.Id, "card.assigned", new { boardId = board.Id, cardId = card.Id, assigneeId = card.AssigneeId });
        return BuildCardDetails(card);
    }

    public CardDetailsResponse UpdateLabels(Guid userId, Guid cardId, UpdateCardLabelsRequest request)
    {
        var (workspace, board, card) = LoadCardContext(cardId);
        AccessGuard.EnsureBoardContributor(workspace, board, userId);

        var allowedLabels = repository.GetLabelsByWorkspace(workspace.Id).Select(label => label.Id).ToHashSet();
        if (request.LabelIds.Any(labelId => !allowedLabels.Contains(labelId)))
        {
            throw new ValidationAppException("All labels must belong to the card workspace.");
        }

        card.LabelIds.Clear();
        card.LabelIds.AddRange(request.LabelIds.Distinct());
        card.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        repository.UpdateCard(card);

        AddActivity(workspace.Id, board.Id, card.Id, userId, ActivityType.CardLabelsUpdated, $"Updated labels for card '{card.Title}'.");
        repository.SaveChanges();
        PublishBoardEvent(board.Id, "card.labels-updated", new { boardId = board.Id, cardId = card.Id });
        return BuildCardDetails(card);
    }

    public CommentResponse AddComment(Guid userId, Guid cardId, CreateCommentRequest request)
    {
        var (workspace, board, card) = LoadCardContext(cardId);
        AccessGuard.EnsureBoardContributor(workspace, board, userId);

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ValidationAppException("Comment content is required.");
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var comment = new Comment
        {
            CardId = card.Id,
            AuthorId = userId,
            Content = request.Content.Trim(),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        repository.AddComment(comment);
        AddActivity(workspace.Id, board.Id, card.Id, userId, ActivityType.CommentAdded, $"Added a comment to card '{card.Title}'.");

        if (card.AssigneeId.HasValue && card.AssigneeId.Value != userId)
        {
            AddNotification(card.AssigneeId.Value, NotificationType.Comment, $"A new comment was added to card '{card.Title}'.", card.Id);
        }

        repository.SaveChanges();
        PublishBoardEvent(board.Id, "comment.created", new { boardId = board.Id, cardId = card.Id, commentId = comment.Id });
        return comment.ToResponse(repository.GetUserById);
    }

    public CommentResponse UpdateComment(Guid userId, Guid commentId, UpdateCommentRequest request)
    {
        var comment = repository.GetComment(commentId)
            ?? throw new NotFoundException("Comment not found.");
        var (workspace, board, card) = LoadCardContext(comment.CardId);
        AccessGuard.EnsureCanModerateComment(workspace, board, comment, userId);

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ValidationAppException("Comment content is required.");
        }

        comment.Content = request.Content.Trim();
        comment.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        repository.UpdateComment(comment);
        AddActivity(workspace.Id, board.Id, card.Id, userId, ActivityType.CommentUpdated, $"Updated a comment on card '{card.Title}'.");
        repository.SaveChanges();
        PublishBoardEvent(board.Id, "comment.updated", new { boardId = board.Id, cardId = card.Id, commentId = comment.Id });

        return comment.ToResponse(repository.GetUserById);
    }

    public void DeleteComment(Guid userId, Guid commentId)
    {
        var comment = repository.GetComment(commentId)
            ?? throw new NotFoundException("Comment not found.");
        var (workspace, board, card) = LoadCardContext(comment.CardId);
        AccessGuard.EnsureCanModerateComment(workspace, board, comment, userId);

        repository.DeleteComment(commentId);
        AddActivity(workspace.Id, board.Id, card.Id, userId, ActivityType.CommentDeleted, $"Deleted a comment from card '{card.Title}'.");
        repository.SaveChanges();
        PublishBoardEvent(board.Id, "comment.deleted", new { boardId = board.Id, cardId = card.Id, commentId });
    }

    public ChecklistItemResponse AddChecklistItem(Guid userId, Guid cardId, AddChecklistItemRequest request)
    {
        var (workspace, board, card) = LoadCardContext(cardId);
        AccessGuard.EnsureCanEditCard(workspace, board, card, userId);

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ValidationAppException("Checklist item title is required.");
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var item = new ChecklistItem
        {
            Title = request.Title.Trim(),
            Position = card.ChecklistItems.Count,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        card.ChecklistItems.Add(item);
        card.UpdatedAtUtc = utcNow;
        repository.UpdateCard(card);
        AddActivity(workspace.Id, board.Id, card.Id, userId, ActivityType.ChecklistItemAdded, $"Added checklist item '{item.Title}' to card '{card.Title}'.");
        repository.SaveChanges();
        PublishBoardEvent(board.Id, "checklist.created", new { boardId = board.Id, cardId = card.Id, itemId = item.Id });
        return item.ToResponse();
    }

    public ChecklistItemResponse UpdateChecklistItem(Guid userId, Guid checklistItemId, UpdateChecklistItemRequest request)
    {
        var (card, item) = FindChecklistItem(checklistItemId);
        var (workspace, board, _) = LoadCardContext(card.Id);
        AccessGuard.EnsureCanEditCard(workspace, board, card, userId);

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            item.Title = request.Title.Trim();
        }

        if (request.IsCompleted.HasValue)
        {
            item.IsCompleted = request.IsCompleted.Value;
        }

        item.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        card.UpdatedAtUtc = item.UpdatedAtUtc;
        repository.UpdateCard(card);
        AddActivity(workspace.Id, board.Id, card.Id, userId, ActivityType.ChecklistItemUpdated, $"Updated checklist item '{item.Title}' on card '{card.Title}'.");
        repository.SaveChanges();
        PublishBoardEvent(board.Id, "checklist.updated", new { boardId = board.Id, cardId = card.Id, itemId = item.Id });
        return item.ToResponse();
    }

    public void DeleteChecklistItem(Guid userId, Guid checklistItemId)
    {
        var (card, item) = FindChecklistItem(checklistItemId);
        var (workspace, board, _) = LoadCardContext(card.Id);
        AccessGuard.EnsureCanEditCard(workspace, board, card, userId);

        card.ChecklistItems.RemoveAll(existing => existing.Id == checklistItemId);
        for (var index = 0; index < card.ChecklistItems.Count; index++)
        {
            card.ChecklistItems[index].Position = index;
        }

        card.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        repository.UpdateCard(card);
        AddActivity(workspace.Id, board.Id, card.Id, userId, ActivityType.ChecklistItemDeleted, $"Deleted checklist item '{item.Title}' from card '{card.Title}'.");
        repository.SaveChanges();
        PublishBoardEvent(board.Id, "checklist.deleted", new { boardId = board.Id, cardId = card.Id, itemId = checklistItemId });
    }

    private CardDetailsResponse BuildCardDetails(TaskCard card)
    {
        var comments = repository.GetCommentsByCard(card.Id);
        return card.ToDetailsResponse(comments, repository.GetUserById);
    }

    private (Workspace Workspace, Board Board) LoadBoardContext(Guid boardId)
    {
        var board = repository.GetBoard(boardId)
            ?? throw new NotFoundException("Board not found.");
        var workspace = repository.GetWorkspace(board.WorkspaceId)
            ?? throw new NotFoundException("Workspace not found.");
        return (workspace, board);
    }

    private (Workspace Workspace, Board Board, TaskCard Card) LoadCardContext(Guid cardId)
    {
        var card = repository.GetCard(cardId)
            ?? throw new NotFoundException("Card not found.");
        var (workspace, board) = LoadBoardContext(card.BoardId);
        return (workspace, board, card);
    }

    private (TaskCard Card, ChecklistItem Item) FindChecklistItem(Guid checklistItemId)
    {
        foreach (var card in repository.GetAllCards())
        {
            var item = card.ChecklistItems.FirstOrDefault(existing => existing.Id == checklistItemId);
            if (item is not null)
            {
                return (card, item);
            }
        }

        throw new NotFoundException("Checklist item not found.");
    }

    private void ReindexCards(List<TaskCard> cards, DateTime utcNow)
    {
        for (var index = 0; index < cards.Count; index++)
        {
            cards[index].Position = index;
            cards[index].UpdatedAtUtc = utcNow;
            repository.UpdateCard(cards[index]);
        }
    }

    private void EnsureAssigneeCanBeUsed(Workspace workspace, Board board, Guid? assigneeId)
    {
        if (!assigneeId.HasValue)
        {
            return;
        }

        if (!AccessGuard.IsWorkspaceMember(workspace, assigneeId.Value))
        {
            throw new ValidationAppException("Assignee must be a workspace member.");
        }

        if (!AccessGuard.HasBoardAccess(workspace, board, assigneeId.Value))
        {
            throw new ValidationAppException("Assignee must have access to the board.");
        }
    }

    private void ValidateCardPayload(string title, DateTime? deadlineUtc)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ValidationAppException("Card title is required.");
        }

        if (deadlineUtc.HasValue && deadlineUtc.Value < timeProvider.GetUtcNow().UtcDateTime)
        {
            throw new ValidationAppException("Deadline cannot be earlier than the creation date.");
        }
    }

    private void AddNotification(Guid userId, NotificationType type, string message, Guid entityId)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        repository.AddNotification(new Notification
        {
            UserId = userId,
            Type = type,
            Message = message,
            RelatedEntityId = entityId,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        });
    }

    private void AddActivity(Guid workspaceId, Guid boardId, Guid cardId, Guid actorId, ActivityType type, string description)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        repository.AddActivity(new ActivityLog
        {
            WorkspaceId = workspaceId,
            BoardId = boardId,
            CardId = cardId,
            ActorId = actorId,
            Type = type,
            Description = description,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        });
    }

    private void PublishBoardEvent(Guid boardId, string eventName, object payload)
    {
        realtimeNotifier.PublishBoardEventAsync(boardId, eventName, payload).GetAwaiter().GetResult();
    }
}
