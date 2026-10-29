namespace TrainSchedule.Board.Consumer;

public sealed record TripView(Guid Id, string TrainNumber, string DepartureStation,
    string ArrivalStation, DateTimeOffset DepartureTime, DateTimeOffset ArrivalTime, string Platform)
{
    public string Status { get; init; } = "Scheduled";
    public int DelayMinutes { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTimeOffset? ActualArrivalTime { get; init; }

    public override string ToString() =>
        $"[BOARD] {Id} | Train {TrainNumber}: {DepartureStation} -> {ArrivalStation} | " +
        $"Departure: {DepartureTime.AddMinutes(DelayMinutes):yyyy-MM-dd HH:mm zzz} | " +
        $"Arrival: {ArrivalTime.AddMinutes(DelayMinutes):yyyy-MM-dd HH:mm zzz} | " +
        $"Platform: {Platform} | {Status} | Delay: {DelayMinutes} min" +
        (string.IsNullOrEmpty(Reason) ? string.Empty : $" | Reason: {Reason}") +
        (ActualArrivalTime is null ? string.Empty : $" | Actual arrival: {ActualArrivalTime:yyyy-MM-dd HH:mm zzz}");
}
