namespace TrainSchedule.Producer.Api.Events;

public sealed record TripPlatformChanged(
    Guid EventId,
    Guid TripId,
    DateTimeOffset OccurredAt,
    string OldPlatform,
    string NewPlatform) : IEvent;
