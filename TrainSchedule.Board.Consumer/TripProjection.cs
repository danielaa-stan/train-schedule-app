using System.Text.Json;

namespace TrainSchedule.Board.Consumer;

public sealed class TripProjection
{
    private readonly Dictionary<Guid, TripView> _trips = [];
    private readonly HashSet<Guid> _processedEvents = [];

    // Called sequentially. Null means the event has already been processed.
    public TripView? Apply(string eventType, JsonElement data)
    {
        var eventId = data.GetProperty("EventId").GetGuid();
        var tripId = data.GetProperty("TripId").GetGuid();
        if (_processedEvents.Contains(eventId)) return null;

        TripView updated;
        if (eventType == "TripCreated")
        {
            if (_trips.ContainsKey(tripId))
                throw new InvalidOperationException("Trip already exists in the projection.");

            updated = new TripView(tripId, Text(data, "TrainNumber"),
                Text(data, "DepartureStation"), Text(data, "ArrivalStation"),
                data.GetProperty("DepartureTime").GetDateTimeOffset(),
                data.GetProperty("ArrivalTime").GetDateTimeOffset(), Text(data, "Platform"));
        }
        else
        {
            if (!_trips.TryGetValue(tripId, out var current))
                throw new InvalidOperationException($"Trip {tripId} has no creation event in the projection.");

            updated = eventType switch
            {
                "TripDelayed" => current with
                {
                    Status = "Delayed", DelayMinutes = data.GetProperty("DelayMinutes").GetInt32(),
                    Reason = Text(data, "Reason")
                },
                "TripPlatformChanged" => current with { Platform = Text(data, "NewPlatform") },
                "TripCancelled" => current with { Status = "Cancelled", Reason = Text(data, "Reason") },
                "TripRestored" => current with { Status = "Scheduled", DelayMinutes = 0, Reason = string.Empty },
                "TripCompleted" => current with
                {
                    Status = "Completed", ActualArrivalTime = data.GetProperty("ActualArrivalTime").GetDateTimeOffset()
                },
                _ => throw new ArgumentException($"Unknown event type: {eventType}.")
            };
        }

        _trips[tripId] = updated;
        _processedEvents.Add(eventId);
        return updated;
    }

    private static string Text(JsonElement data, string name) =>
        data.GetProperty(name).GetString() ?? throw new JsonException($"{name} must not be null.");
}
