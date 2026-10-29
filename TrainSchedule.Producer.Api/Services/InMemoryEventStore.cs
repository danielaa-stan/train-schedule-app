using TrainSchedule.Producer.Api.Events;

namespace TrainSchedule.Producer.Api.Services;

public sealed class InMemoryEventStore
{
    private readonly Dictionary<Guid, List<IEvent>> _store = [];
    private readonly object _sync = new();

    public IReadOnlyList<IEvent> GetEvents(Guid tripId)
    {
        lock (_sync)
        {
            // Return a snapshot so callers cannot change the stored history.
            return _store.TryGetValue(tripId, out var history)
                ? history.ToArray()
                : Array.Empty<IEvent>();
        }
    }

    public void AppendEvents(Guid tripId, IEnumerable<IEvent> events, int expectedVersion)
    {
        if (tripId == Guid.Empty)
        {
            throw new ArgumentException("Trip ID must not be empty.", nameof(tripId));
        }

        ArgumentNullException.ThrowIfNull(events);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion);

        var newEvents = events.ToArray();
        if (newEvents.Any(e => e is null || e.TripId != tripId || e.EventId == Guid.Empty))
        {
            throw new ArgumentException("All events must have an event ID and belong to the specified trip.", nameof(events));
        }

        lock (_sync)
        {
            _store.TryGetValue(tripId, out var history);
            var currentVersion = history?.Count ?? 0;

            // Reject changes based on stale history, including concurrent creation.
            if (currentVersion != expectedVersion)
            {
                throw new InvalidOperationException(
                    "Trip history has changed. Reload the trip and retry the command.");
            }

            var eventIds = history is null
                ? new HashSet<Guid>()
                : history.Select(e => e.EventId).ToHashSet();

            if (newEvents.Any(e => !eventIds.Add(e.EventId)))
            {
                throw new ArgumentException("An event cannot be stored more than once.", nameof(events));
            }

            if (newEvents.Length == 0)
            {
                return;
            }

            if (history is null)
            {
                history = [];
                _store.Add(tripId, history);
            }

            history.AddRange(newEvents);
        }
    }
}
