namespace TrainSchedule.Producer.Api.Dto;

public sealed class CancelTripRequest
{
    public string Reason { get; init; } = string.Empty;
}
