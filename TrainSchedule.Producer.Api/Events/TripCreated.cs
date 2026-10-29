namespace TrainSchedule.Producer.Api.Events;

public sealed record TripCreated(
    Guid EventId,
    Guid TripId,
    DateTimeOffset OccurredAt,
    string TrainNumber,
    string DepartureStation,
    string ArrivalStation,
    DateTimeOffset DepartureTime,
    DateTimeOffset ArrivalTime,
    string Platform) : IEvent;
