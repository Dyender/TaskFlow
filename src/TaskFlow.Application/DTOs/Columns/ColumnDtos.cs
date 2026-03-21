using TaskFlow.Application.DTOs.Cards;

namespace TaskFlow.Application.DTOs.Columns;

public sealed record CreateColumnRequest(string Title, int? Position);

public sealed record UpdateColumnRequest(string? Title, bool? IsArchived);

public sealed record ReorderColumnsRequest(Guid BoardId, IReadOnlyCollection<ColumnOrderItem> Columns);

public sealed record ColumnOrderItem(Guid ColumnId, int Position);

public sealed record ColumnResponse(Guid Id, Guid BoardId, string Title, int Position, bool IsArchived, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record ColumnWithCardsResponse(
    Guid Id,
    Guid BoardId,
    string Title,
    int Position,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyCollection<CardResponse> Cards);
