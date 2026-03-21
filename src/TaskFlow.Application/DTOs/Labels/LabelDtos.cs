namespace TaskFlow.Application.DTOs.Labels;

public sealed record CreateLabelRequest(string Name, string Color);

public sealed record LabelResponse(Guid Id, Guid WorkspaceId, string Name, string Color, DateTime CreatedAtUtc);
