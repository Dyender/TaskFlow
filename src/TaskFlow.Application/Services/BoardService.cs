using TaskFlow.Application.Contracts;
using TaskFlow.Application.DTOs.Boards;
using TaskFlow.Application.DTOs.Cards;
using TaskFlow.Application.DTOs.Columns;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.Services;

public sealed class BoardService(
    ITaskFlowRepository repository,
    IBoardRealtimeNotifier realtimeNotifier,
    TimeProvider timeProvider) : IBoardService
{
    public BoardDetailsResponse GetById(Guid userId, Guid boardId)
    {
        var (workspace, board) = LoadBoardContext(boardId);
        AccessGuard.EnsureCanViewBoard(workspace, board, userId);
        return BuildBoardDetails(board);
    }

    public BoardDetailsResponse Create(Guid userId, Guid workspaceId, CreateBoardRequest request)
    {
        var workspace = repository.GetWorkspace(workspaceId)
            ?? throw new NotFoundException("Workspace not found.");

        AccessGuard.EnsureWorkspaceContributor(workspace, userId);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationAppException("Board name is required.");
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var board = new Board
        {
            WorkspaceId = workspaceId,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Color = string.IsNullOrWhiteSpace(request.Color) ? "#2563EB" : request.Color.Trim(),
            Visibility = request.Visibility,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
            Members =
            {
                new BoardMember
                {
                    UserId = userId,
                    Role = WorkspaceRole.Admin,
                    JoinedAtUtc = utcNow
                }
            }
        };

        repository.AddBoard(board);

        if (request.CreateDefaultColumns)
        {
            var defaultColumns = new[] { "Backlog", "To Do", "In Progress", "Review", "Done" };
            for (var index = 0; index < defaultColumns.Length; index++)
            {
                repository.AddColumn(new BoardColumn
                {
                    BoardId = board.Id,
                    Title = defaultColumns[index],
                    Position = index,
                    CreatedAtUtc = utcNow,
                    UpdatedAtUtc = utcNow
                });
            }
        }

        repository.SaveChanges();
        PublishBoardEvent(board.Id, "board.created", new { boardId = board.Id, board.Name });
        return BuildBoardDetails(board);
    }

    public BoardDetailsResponse Update(Guid userId, Guid boardId, UpdateBoardRequest request)
    {
        var (workspace, board) = LoadBoardContext(boardId);
        AccessGuard.EnsureBoardManager(workspace, board, userId);

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            board.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            board.Description = request.Description.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Color))
        {
            board.Color = request.Color.Trim();
        }

        if (request.Visibility.HasValue)
        {
            board.Visibility = request.Visibility.Value;
        }

        if (request.IsArchived.HasValue)
        {
            board.IsArchived = request.IsArchived.Value;
        }

        board.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        repository.UpdateBoard(board);
        repository.SaveChanges();

        PublishBoardEvent(board.Id, "board.updated", new { boardId = board.Id, board.Name, board.IsArchived });
        return BuildBoardDetails(board);
    }

    public void Archive(Guid userId, Guid boardId)
    {
        var (workspace, board) = LoadBoardContext(boardId);
        AccessGuard.EnsureBoardManager(workspace, board, userId);

        board.IsArchived = true;
        board.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        repository.UpdateBoard(board);
        repository.SaveChanges();

        PublishBoardEvent(board.Id, "board.archived", new { boardId = board.Id });
    }

    public BoardDetailsResponse AddMember(Guid userId, Guid boardId, AddBoardMemberRequest request)
    {
        var (workspace, board) = LoadBoardContext(boardId);
        AccessGuard.EnsureBoardManager(workspace, board, userId);

        if (!AccessGuard.IsWorkspaceMember(workspace, request.UserId))
        {
            throw new ValidationAppException("User must be a workspace member before being added to the board.");
        }

        if (board.Members.FirstOrDefault(member => member.UserId == request.UserId) is { } existingMember)
        {
            existingMember.Role = request.Role;
        }
        else
        {
            var utcNow = timeProvider.GetUtcNow().UtcDateTime;
            board.Members.Add(new BoardMember
            {
                UserId = request.UserId,
                Role = request.Role,
                JoinedAtUtc = utcNow
            });

            repository.AddNotification(new Notification
            {
                UserId = request.UserId,
                Type = NotificationType.AddedToBoard,
                Message = $"You were added to board '{board.Name}' as {request.Role}.",
                RelatedEntityId = board.Id,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow
            });
        }

        board.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        repository.UpdateBoard(board);
        repository.SaveChanges();

        PublishBoardEvent(board.Id, "board.member-added", new { boardId = board.Id, request.UserId, role = request.Role.ToString() });
        return BuildBoardDetails(board);
    }

    public ColumnResponse CreateColumn(Guid userId, Guid boardId, CreateColumnRequest request)
    {
        var (workspace, board) = LoadBoardContext(boardId);
        AccessGuard.EnsureBoardManager(workspace, board, userId);

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ValidationAppException("Column title is required.");
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var columns = repository.GetColumnsByBoard(boardId)
            .OrderBy(column => column.Position)
            .ToList();

        var insertPosition = request.Position.HasValue
            ? Math.Clamp(request.Position.Value, 0, columns.Count)
            : columns.Count;

        var column = new BoardColumn
        {
            BoardId = boardId,
            Title = request.Title.Trim(),
            Position = insertPosition,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        columns.Insert(insertPosition, column);
        ReindexColumns(columns, utcNow);
        repository.AddColumn(column);
        repository.SaveChanges();

        PublishBoardEvent(board.Id, "column.created", new { boardId = board.Id, columnId = column.Id });
        return column.ToResponse();
    }

    public ColumnResponse UpdateColumn(Guid userId, Guid columnId, UpdateColumnRequest request)
    {
        var column = repository.GetColumn(columnId)
            ?? throw new NotFoundException("Column not found.");
        var (workspace, board) = LoadBoardContext(column.BoardId);
        AccessGuard.EnsureBoardManager(workspace, board, userId);

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            column.Title = request.Title.Trim();
        }

        if (request.IsArchived.HasValue)
        {
            column.IsArchived = request.IsArchived.Value;
        }

        column.UpdatedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        repository.UpdateColumn(column);
        repository.SaveChanges();

        PublishBoardEvent(board.Id, "column.updated", new { boardId = board.Id, columnId = column.Id, column.Title, column.IsArchived });
        return column.ToResponse();
    }

    public void DeleteColumn(Guid userId, Guid columnId)
    {
        var column = repository.GetColumn(columnId)
            ?? throw new NotFoundException("Column not found.");

        var (workspace, board) = LoadBoardContext(column.BoardId);
        AccessGuard.EnsureBoardManager(workspace, board, userId);

        if (repository.GetCardsByColumn(columnId).Any())
        {
            throw new ValidationAppException("Cannot delete a column that still contains cards.");
        }

        repository.DeleteColumn(columnId);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        ReindexColumns(repository.GetColumnsByBoard(board.Id).OrderBy(item => item.Position).ToList(), utcNow);
        repository.SaveChanges();

        PublishBoardEvent(board.Id, "column.deleted", new { boardId = board.Id, columnId });
    }

    public IReadOnlyCollection<ColumnResponse> ReorderColumns(Guid userId, ReorderColumnsRequest request)
    {
        var (workspace, board) = LoadBoardContext(request.BoardId);
        AccessGuard.EnsureBoardManager(workspace, board, userId);

        var existingColumns = repository.GetColumnsByBoard(board.Id).OrderBy(column => column.Position).ToList();
        if (existingColumns.Count != request.Columns.Count || existingColumns.Any(column => request.Columns.All(item => item.ColumnId != column.Id)))
        {
            throw new ValidationAppException("The reorder payload must contain all board columns exactly once.");
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var item in request.Columns.OrderBy(item => item.Position))
        {
            var column = existingColumns.First(existing => existing.Id == item.ColumnId);
            column.Position = item.Position;
            column.UpdatedAtUtc = utcNow;
            repository.UpdateColumn(column);
        }

        ReindexColumns(existingColumns.OrderBy(column => column.Position).ToList(), utcNow);
        repository.SaveChanges();

        PublishBoardEvent(board.Id, "column.reordered", new { boardId = board.Id });
        return repository.GetColumnsByBoard(board.Id)
            .OrderBy(column => column.Position)
            .Select(column => column.ToResponse())
            .ToArray();
    }

    public IReadOnlyCollection<CardResponse> SearchBoardCards(Guid userId, Guid boardId, BoardCardsQuery query)
    {
        var (workspace, board) = LoadBoardContext(boardId);
        AccessGuard.EnsureCanViewBoard(workspace, board, userId);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var cards = repository.GetCardsByBoard(boardId).AsEnumerable();

        if (!query.IncludeArchived)
        {
            cards = cards.Where(card => !card.IsArchived);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            cards = cards.Where(card =>
                card.Title.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                card.Description.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (query.AssigneeId.HasValue)
        {
            cards = cards.Where(card => card.AssigneeId == query.AssigneeId);
        }

        if (query.Priority.HasValue)
        {
            cards = cards.Where(card => card.Priority == query.Priority);
        }

        if (query.LabelIds is { Length: > 0 })
        {
            cards = cards.Where(card => query.LabelIds.All(labelId => card.LabelIds.Contains(labelId)));
        }

        if (query.DueBeforeUtc.HasValue)
        {
            cards = cards.Where(card => card.DeadlineUtc.HasValue && card.DeadlineUtc.Value <= query.DueBeforeUtc.Value);
        }

        if (query.OnlyOverdue)
        {
            cards = cards.Where(card => card.DeadlineUtc.HasValue && card.DeadlineUtc.Value < utcNow);
        }

        return cards
            .OrderBy(card => card.DeadlineUtc ?? DateTime.MaxValue)
            .ThenBy(card => card.Position)
            .Select(card => card.ToResponse(repository.GetUserById))
            .ToArray();
    }

    private BoardDetailsResponse BuildBoardDetails(Board board)
    {
        var columns = repository.GetColumnsByBoard(board.Id)
            .OrderBy(column => column.Position)
            .Select(column => new ColumnWithCardsResponse(
                column.Id,
                column.BoardId,
                column.Title,
                column.Position,
                column.IsArchived,
                column.CreatedAtUtc,
                column.UpdatedAtUtc,
                repository.GetCardsByColumn(column.Id)
                    .OrderBy(card => card.Position)
                    .Select(card => card.ToResponse(repository.GetUserById))
                    .ToArray()))
            .ToArray();

        var labels = repository.GetLabelsByWorkspace(board.WorkspaceId)
            .OrderBy(label => label.Name)
            .Select(label => label.ToResponse())
            .ToArray();

        return new BoardDetailsResponse(
            board.Id,
            board.WorkspaceId,
            board.Name,
            board.Description,
            board.Color,
            board.IsArchived,
            board.Visibility,
            board.CreatedAtUtc,
            board.UpdatedAtUtc,
            board.Members
                .OrderByDescending(member => member.Role)
                .ThenBy(member => repository.GetUserById(member.UserId)?.Name)
                .Select(member => member.ToResponse(repository.GetUserById))
                .ToArray(),
            columns,
            labels);
    }

    private (Workspace Workspace, Board Board) LoadBoardContext(Guid boardId)
    {
        var board = repository.GetBoard(boardId)
            ?? throw new NotFoundException("Board not found.");
        var workspace = repository.GetWorkspace(board.WorkspaceId)
            ?? throw new NotFoundException("Workspace not found.");

        return (workspace, board);
    }

    private void ReindexColumns(List<BoardColumn> columns, DateTime utcNow)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            columns[index].Position = index;
            columns[index].UpdatedAtUtc = utcNow;
            repository.UpdateColumn(columns[index]);
        }
    }

    private void PublishBoardEvent(Guid boardId, string eventName, object payload)
    {
        realtimeNotifier.PublishBoardEventAsync(boardId, eventName, payload).GetAwaiter().GetResult();
    }
}
