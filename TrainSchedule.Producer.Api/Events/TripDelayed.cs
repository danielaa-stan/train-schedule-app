namespace TrainSchedule.Producer.Api.Events;

public sealed record TripDelayed(
    Guid EventId,
    Guid TripId,
    DateTimeOffset OccurredAt,
    int DelayMinutes,
    string Reason) : IEvent;
