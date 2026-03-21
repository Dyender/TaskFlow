using TaskFlow.Application.DTOs.Users;

namespace TaskFlow.Application.DTOs.Comments;

public sealed record CreateCommentRequest(string Content);

public sealed record UpdateCommentRequest(string Content);

public sealed record CommentResponse(Guid Id, Guid CardId, UserSummaryResponse Author, string Content, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
