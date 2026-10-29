using TrainSchedule.Producer.Api.Events;

namespace TrainSchedule.Producer.Api.Domain;

public sealed class TripAggregate
{
    private readonly List<IEvent> _uncommittedEvents = [];

    public Guid Id { get; private set; }
    public bool IsCreated { get; private set; }
    public string TrainNumber { get; private set; } = string.Empty;
    public string DepartureStation { get; private set; } = string.Empty;
    public string ArrivalStation { get; private set; } = string.Empty;
    public DateTimeOffset DepartureTime { get; private set; }
    public DateTimeOffset ArrivalTime { get; private set; }
    public string Platform { get; private set; } = string.Empty;
    public TripStatus Status { get; private set; }
    public int DelayMinutes { get; private set; }
    public DateTimeOffset? ActualArrivalTime { get; private set; }

    public IReadOnlyList<IEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    public TripAggregate(IEnumerable<IEvent> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        // Replaying history restores state without generating new events.
        foreach (var @event in history)
        {
            Apply(@event);
        }
    }

    public void Create(Guid tripId, string trainNumber, string departureStation, string arrivalStation,
        DateTimeOffset departureTime, DateTimeOffset arrivalTime, string platform)
    {
        if (IsCreated)
        {
            throw new InvalidOperationException("Trip already exists.");
        }

        if (tripId == Guid.Empty)
        {
            throw new ArgumentException("Trip ID must not be empty.", nameof(tripId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(trainNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(departureStation);
        ArgumentException.ThrowIfNullOrWhiteSpace(arrivalStation);
        ArgumentException.ThrowIfNullOrWhiteSpace(platform);

        if (arrivalTime <= departureTime)
        {
            throw new ArgumentException("Arrival time must be after departure time.", nameof(arrivalTime));
        }

        Raise(new TripCreated(Guid.NewGuid(), tripId, DateTimeOffset.UtcNow,
            trainNumber, departureStation, arrivalStation, departureTime, arrivalTime, platform));
    }

    public void Delay(int delayMinutes, string reason)
    {
        EnsureActive();

        if (delayMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delayMinutes), "Delay must be greater than zero minutes.");
        }

        Raise(new TripDelayed(Guid.NewGuid(), Id, DateTimeOffset.UtcNow, delayMinutes, reason));
    }

    public void ChangePlatform(string newPlatform)
    {
        EnsureActive();
        ArgumentException.ThrowIfNullOrWhiteSpace(newPlatform);

        Raise(new TripPlatformChanged(Guid.NewGuid(), Id, DateTimeOffset.UtcNow, Platform, newPlatform));
    }

    public void Cancel(string reason)
    {
        EnsureActive();

        Raise(new TripCancelled(Guid.NewGuid(), Id, DateTimeOffset.UtcNow, reason));
    }

    public void Restore()
    {
        EnsureCreated();

        if (Status is not (TripStatus.Cancelled or TripStatus.Delayed))
        {
            throw new InvalidOperationException("Only a cancelled or delayed trip can be restored.");
        }

        Raise(new TripRestored(Guid.NewGuid(), Id, DateTimeOffset.UtcNow));
    }

    public void Complete(DateTimeOffset actualArrivalTime)
    {
        EnsureActive();

        if (actualArrivalTime < DepartureTime)
        {
            throw new ArgumentException("Actual arrival time must not be before departure time.", nameof(actualArrivalTime));
        }

        Raise(new TripCompleted(Guid.NewGuid(), Id, DateTimeOffset.UtcNow, actualArrivalTime));
    }

    // Call only after all pending events have been successfully persisted.
    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();

    private void EnsureCreated()
    {
        if (!IsCreated)
        {
            throw new InvalidOperationException("Trip does not exist.");
        }
    }

    private void EnsureActive()
    {
        EnsureCreated();

        if (Status is TripStatus.Cancelled or TripStatus.Completed)
        {
            throw new InvalidOperationException("A cancelled or completed trip cannot be changed.");
        }
    }

    private void Raise(IEvent @event)
    {
        Apply(@event);
        _uncommittedEvents.Add(@event);
    }

    private void Apply(IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);

        if (@event is TripCreated && IsCreated)
        {
            throw new InvalidOperationException("Trip history contains more than one creation event.");
        }

        if (@event is not TripCreated)
        {
            EnsureCreated();
        }

        if (IsCreated && @event.TripId != Id)
        {
            throw new InvalidOperationException("Event belongs to a different trip.");
        }

        switch (@event)
        {
            case TripCreated e:
                Id = e.TripId;
                TrainNumber = e.TrainNumber;
                DepartureStation = e.DepartureStation;
                ArrivalStation = e.ArrivalStation;
                DepartureTime = e.DepartureTime;
                ArrivalTime = e.ArrivalTime;
                Platform = e.Platform;
                Status = TripStatus.Scheduled;
                IsCreated = true;
                break;
            case TripDelayed e:
                // This is the total delay, not an increment. Scheduled times stay unchanged.
                DelayMinutes = e.DelayMinutes;
                Status = TripStatus.Delayed;
                break;
            case TripPlatformChanged e:
                Platform = e.NewPlatform;
                break;
            case TripCancelled:
                Status = TripStatus.Cancelled;
                break;
            case TripRestored:
                Status = TripStatus.Scheduled;
                DelayMinutes = 0;
                break;
            case TripCompleted e:
                Status = TripStatus.Completed;
                ActualArrivalTime = e.ActualArrivalTime;
                break;
            default:
                throw new ArgumentException($"Unsupported trip event: {@event.GetType().Name}.", nameof(@event));
        }
    }
}
