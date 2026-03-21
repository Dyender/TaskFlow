using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class EfTaskFlowRepository(TaskFlowDbContext dbContext) : ITaskFlowRepository
{
    public IReadOnlyCollection<User> GetUsers() =>
        dbContext.Users.OrderBy(user => user.Name).ToList();

    public User? GetUserById(Guid userId) =>
        dbContext.Users.FirstOrDefault(user => user.Id == userId);

    public User? GetUserByEmail(string email) =>
        dbContext.Users.FirstOrDefault(user => user.Email == email);

    public void AddUser(User user) => dbContext.Users.Add(user);

    public void UpdateUser(User user) => AttachIfNeeded(user, dbContext.Users);

    public IReadOnlyCollection<RefreshSession> GetRefreshSessionsByUser(Guid userId) =>
        dbContext.RefreshSessions.Where(session => session.UserId == userId).ToList();

    public RefreshSession? GetRefreshSessionByTokenHash(string tokenHash) =>
        dbContext.RefreshSessions.FirstOrDefault(session => session.TokenHash == tokenHash);

    public void AddRefreshSession(RefreshSession session) => dbContext.RefreshSessions.Add(session);

    public void UpdateRefreshSession(RefreshSession session) => AttachIfNeeded(session, dbContext.RefreshSessions);

    public IReadOnlyCollection<Workspace> GetAllWorkspaces() =>
        dbContext.Workspaces.OrderBy(workspace => workspace.Name).ToList();

    public IReadOnlyCollection<Workspace> GetWorkspacesForUser(Guid userId) =>
        dbContext.Workspaces
            .Where(workspace => workspace.Members.Any(member => member.UserId == userId))
            .OrderBy(workspace => workspace.Name)
            .ToList();

    public Workspace? GetWorkspace(Guid workspaceId) =>
        dbContext.Workspaces.FirstOrDefault(workspace => workspace.Id == workspaceId);

    public void AddWorkspace(Workspace workspace) => dbContext.Workspaces.Add(workspace);

    public void UpdateWorkspace(Workspace workspace) => AttachIfNeeded(workspace, dbContext.Workspaces);

    public IReadOnlyCollection<Board> GetAllBoards() =>
        dbContext.Boards.OrderBy(board => board.Name).ToList();

    public IReadOnlyCollection<Board> GetBoardsByWorkspace(Guid workspaceId) =>
        dbContext.Boards.Where(board => board.WorkspaceId == workspaceId).OrderBy(board => board.Name).ToList();

    public Board? GetBoard(Guid boardId) =>
        dbContext.Boards.FirstOrDefault(board => board.Id == boardId);

    public void AddBoard(Board board) => dbContext.Boards.Add(board);

    public void UpdateBoard(Board board) => AttachIfNeeded(board, dbContext.Boards);

    public IReadOnlyCollection<BoardColumn> GetColumnsByBoard(Guid boardId) =>
        dbContext.Columns.Where(column => column.BoardId == boardId).OrderBy(column => column.Position).ToList();

    public BoardColumn? GetColumn(Guid columnId) =>
        dbContext.Columns.FirstOrDefault(column => column.Id == columnId);

    public void AddColumn(BoardColumn column) => dbContext.Columns.Add(column);

    public void UpdateColumn(BoardColumn column) => AttachIfNeeded(column, dbContext.Columns);

    public void DeleteColumn(Guid columnId)
    {
        var column = GetColumn(columnId);
        if (column is not null)
        {
            dbContext.Columns.Remove(column);
        }
    }

    public IReadOnlyCollection<TaskCard> GetAllCards() =>
        dbContext.Cards.OrderBy(card => card.CreatedAtUtc).ToList();

    public IReadOnlyCollection<TaskCard> GetCardsByBoard(Guid boardId) =>
        dbContext.Cards.Where(card => card.BoardId == boardId).OrderBy(card => card.Position).ToList();

    public IReadOnlyCollection<TaskCard> GetCardsByColumn(Guid columnId) =>
        dbContext.Cards.Where(card => card.ColumnId == columnId).OrderBy(card => card.Position).ToList();

    public TaskCard? GetCard(Guid cardId) =>
        dbContext.Cards.FirstOrDefault(card => card.Id == cardId);

    public void AddCard(TaskCard card) => dbContext.Cards.Add(card);

    public void UpdateCard(TaskCard card) => AttachIfNeeded(card, dbContext.Cards);

    public void DeleteCard(Guid cardId)
    {
        var card = GetCard(cardId);
        if (card is not null)
        {
            dbContext.Cards.Remove(card);
        }
    }

    public IReadOnlyCollection<Comment> GetCommentsByCard(Guid cardId) =>
        dbContext.Comments.Where(comment => comment.CardId == cardId).OrderBy(comment => comment.CreatedAtUtc).ToList();

    public Comment? GetComment(Guid commentId) =>
        dbContext.Comments.FirstOrDefault(comment => comment.Id == commentId);

    public void AddComment(Comment comment) => dbContext.Comments.Add(comment);

    public void UpdateComment(Comment comment) => AttachIfNeeded(comment, dbContext.Comments);

    public void DeleteComment(Guid commentId)
    {
        var comment = GetComment(commentId);
        if (comment is not null)
        {
            dbContext.Comments.Remove(comment);
        }
    }

    public IReadOnlyCollection<Label> GetLabelsByWorkspace(Guid workspaceId) =>
        dbContext.Labels.Where(label => label.WorkspaceId == workspaceId).OrderBy(label => label.Name).ToList();

    public Label? GetLabel(Guid labelId) =>
        dbContext.Labels.FirstOrDefault(label => label.Id == labelId);

    public void AddLabel(Label label) => dbContext.Labels.Add(label);

    public void UpdateLabel(Label label) => AttachIfNeeded(label, dbContext.Labels);

    public IReadOnlyCollection<Notification> GetNotificationsByUser(Guid userId) =>
        dbContext.Notifications.Where(notification => notification.UserId == userId).OrderByDescending(notification => notification.CreatedAtUtc).ToList();

    public Notification? GetNotification(Guid notificationId) =>
        dbContext.Notifications.FirstOrDefault(notification => notification.Id == notificationId);

    public void AddNotification(Notification notification) => dbContext.Notifications.Add(notification);

    public void UpdateNotification(Notification notification) => AttachIfNeeded(notification, dbContext.Notifications);

    public IReadOnlyCollection<ActivityLog> GetActivitiesByCard(Guid cardId) =>
        dbContext.Activities.Where(activity => activity.CardId == cardId).OrderByDescending(activity => activity.CreatedAtUtc).ToList();

    public void AddActivity(ActivityLog activityLog) => dbContext.Activities.Add(activityLog);

    public void SaveChanges() => dbContext.SaveChanges();

    private void AttachIfNeeded<TEntity>(TEntity entity, DbSet<TEntity> dbSet)
        where TEntity : class
    {
        if (dbContext.Entry(entity).State == EntityState.Detached)
        {
            dbSet.Attach(entity);
            dbContext.Entry(entity).State = EntityState.Modified;
        }
    }
}
