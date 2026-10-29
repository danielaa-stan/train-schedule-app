namespace TrainSchedule.Producer.Api.Events;

public interface IEvent
{
    Guid EventId { get; }
    Guid TripId { get; }
    DateTimeOffset OccurredAt { get; }
}
