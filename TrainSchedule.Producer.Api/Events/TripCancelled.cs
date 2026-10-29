namespace TrainSchedule.Producer.Api.Events;

public sealed record TripCancelled(
    Guid EventId,
    Guid TripId,
    DateTimeOffset OccurredAt,
    string Reason) : IEvent;
