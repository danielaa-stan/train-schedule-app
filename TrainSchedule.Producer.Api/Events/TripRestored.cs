namespace TrainSchedule.Producer.Api.Events;

public sealed record TripRestored(
    Guid EventId,
    Guid TripId,
    DateTimeOffset OccurredAt) : IEvent;
