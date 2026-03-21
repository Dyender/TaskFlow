using TaskFlow.Application.Auth;
using TaskFlow.Application.DTOs.Activity;
using TaskFlow.Application.DTOs.Auth;
using TaskFlow.Application.DTOs.Boards;
using TaskFlow.Application.DTOs.Cards;
using TaskFlow.Application.DTOs.Columns;
using TaskFlow.Application.DTOs.Comments;
using TaskFlow.Application.DTOs.Labels;
using TaskFlow.Application.DTOs.Notifications;
using TaskFlow.Application.DTOs.Users;
using TaskFlow.Application.DTOs.Workspaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Application.Contracts;

public interface ITaskFlowRepository
{
    IReadOnlyCollection<User> GetUsers();
    User? GetUserById(Guid userId);
    User? GetUserByEmail(string email);
    void AddUser(User user);
    void UpdateUser(User user);

    IReadOnlyCollection<RefreshSession> GetRefreshSessionsByUser(Guid userId);
    RefreshSession? GetRefreshSessionByTokenHash(string tokenHash);
    void AddRefreshSession(RefreshSession session);
    void UpdateRefreshSession(RefreshSession session);

    IReadOnlyCollection<Workspace> GetAllWorkspaces();
    IReadOnlyCollection<Workspace> GetWorkspacesForUser(Guid userId);
    Workspace? GetWorkspace(Guid workspaceId);
    void AddWorkspace(Workspace workspace);
    void UpdateWorkspace(Workspace workspace);

    IReadOnlyCollection<Board> GetAllBoards();
    IReadOnlyCollection<Board> GetBoardsByWorkspace(Guid workspaceId);
    Board? GetBoard(Guid boardId);
    void AddBoard(Board board);
    void UpdateBoard(Board board);

    IReadOnlyCollection<BoardColumn> GetColumnsByBoard(Guid boardId);
    BoardColumn? GetColumn(Guid columnId);
    void AddColumn(BoardColumn column);
    void UpdateColumn(BoardColumn column);
    void DeleteColumn(Guid columnId);

    IReadOnlyCollection<TaskCard> GetAllCards();
    IReadOnlyCollection<TaskCard> GetCardsByBoard(Guid boardId);
    IReadOnlyCollection<TaskCard> GetCardsByColumn(Guid columnId);
    TaskCard? GetCard(Guid cardId);
    void AddCard(TaskCard card);
    void UpdateCard(TaskCard card);
    void DeleteCard(Guid cardId);

    IReadOnlyCollection<Comment> GetCommentsByCard(Guid cardId);
    Comment? GetComment(Guid commentId);
    void AddComment(Comment comment);
    void UpdateComment(Comment comment);
    void DeleteComment(Guid commentId);

    IReadOnlyCollection<Label> GetLabelsByWorkspace(Guid workspaceId);
    Label? GetLabel(Guid labelId);
    void AddLabel(Label label);
    void UpdateLabel(Label label);

    IReadOnlyCollection<Notification> GetNotificationsByUser(Guid userId);
    Notification? GetNotification(Guid notificationId);
    void AddNotification(Notification notification);
    void UpdateNotification(Notification notification);

    IReadOnlyCollection<ActivityLog> GetActivitiesByCard(Guid cardId);
    void AddActivity(ActivityLog activityLog);

    void SaveChanges();
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    AuthTokens CreateTokens(User user, DateTime utcNow);
    string HashRefreshToken(string refreshToken);
    bool TryValidateAccessToken(string accessToken, out TokenPayload? payload);
}

public interface IBoardRealtimeNotifier
{
    Task PublishBoardEventAsync(Guid boardId, string eventName, object payload, CancellationToken cancellationToken = default);
}

public interface IAuthService
{
    AuthResponse Register(RegisterRequest request);
    AuthResponse Login(LoginRequest request);
    AuthResponse Refresh(RefreshRequest request);
}

public interface IUserService
{
    UserProfileResponse GetProfile(Guid userId);
    UserProfileResponse UpdateProfile(Guid userId, UpdateProfileRequest request);
}

public interface IWorkspaceService
{
    IReadOnlyCollection<WorkspaceResponse> GetForUser(Guid userId);
    WorkspaceDetailsResponse GetById(Guid userId, Guid workspaceId);
    WorkspaceDetailsResponse Create(Guid userId, CreateWorkspaceRequest request);
    WorkspaceDetailsResponse AddMember(Guid userId, Guid workspaceId, AddWorkspaceMemberRequest request);
    IReadOnlyCollection<LabelResponse> GetLabels(Guid userId, Guid workspaceId);
    LabelResponse CreateLabel(Guid userId, Guid workspaceId, CreateLabelRequest request);
}

public interface IBoardService
{
    BoardDetailsResponse GetById(Guid userId, Guid boardId);
    BoardDetailsResponse Create(Guid userId, Guid workspaceId, CreateBoardRequest request);
    BoardDetailsResponse Update(Guid userId, Guid boardId, UpdateBoardRequest request);
    void Archive(Guid userId, Guid boardId);
    BoardDetailsResponse AddMember(Guid userId, Guid boardId, AddBoardMemberRequest request);
    ColumnResponse CreateColumn(Guid userId, Guid boardId, CreateColumnRequest request);
    ColumnResponse UpdateColumn(Guid userId, Guid columnId, UpdateColumnRequest request);
    void DeleteColumn(Guid userId, Guid columnId);
    IReadOnlyCollection<ColumnResponse> ReorderColumns(Guid userId, ReorderColumnsRequest request);
    IReadOnlyCollection<CardResponse> SearchBoardCards(Guid userId, Guid boardId, BoardCardsQuery query);
}

public interface ICardService
{
    CardDetailsResponse Create(Guid userId, Guid columnId, CreateCardRequest request);
    CardDetailsResponse GetById(Guid userId, Guid cardId);
    CardDetailsResponse Update(Guid userId, Guid cardId, UpdateCardRequest request);
    void Delete(Guid userId, Guid cardId);
    CardDetailsResponse Move(Guid userId, Guid cardId, MoveCardRequest request);
    CardDetailsResponse Assign(Guid userId, Guid cardId, AssignCardRequest request);
    CardDetailsResponse UpdateLabels(Guid userId, Guid cardId, UpdateCardLabelsRequest request);
    CommentResponse AddComment(Guid userId, Guid cardId, CreateCommentRequest request);
    CommentResponse UpdateComment(Guid userId, Guid commentId, UpdateCommentRequest request);
    void DeleteComment(Guid userId, Guid commentId);
    ChecklistItemResponse AddChecklistItem(Guid userId, Guid cardId, AddChecklistItemRequest request);
    ChecklistItemResponse UpdateChecklistItem(Guid userId, Guid checklistItemId, UpdateChecklistItemRequest request);
    void DeleteChecklistItem(Guid userId, Guid checklistItemId);
}

public interface INotificationService
{
    IReadOnlyCollection<NotificationResponse> GetForUser(Guid userId);
    NotificationResponse MarkAsRead(Guid userId, Guid notificationId);
}

public interface IActivityService
{
    IReadOnlyCollection<ActivityResponse> GetCardActivity(Guid userId, Guid cardId);
}
