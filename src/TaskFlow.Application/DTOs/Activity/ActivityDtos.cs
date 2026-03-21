using TaskFlow.Domain.Enums;

namespace TaskFlow.Application.DTOs.Activity;

public sealed record ActivityResponse(Guid Id, Guid ActorId, ActivityType Type, string Description, DateTime CreatedAtUtc);
