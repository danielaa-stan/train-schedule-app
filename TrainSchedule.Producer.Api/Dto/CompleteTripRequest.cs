namespace TrainSchedule.Producer.Api.Dto;

public sealed class CompleteTripRequest
{
    public required DateTimeOffset ActualArrivalTime { get; init; }
}
