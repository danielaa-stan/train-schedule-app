namespace TrainSchedule.Producer.Api.Events;

public sealed record TripCompleted(
    Guid EventId,
    Guid TripId,
    DateTimeOffset OccurredAt,
    DateTimeOffset ActualArrivalTime) : IEvent;
