using System.ComponentModel.DataAnnotations;

namespace TrainSchedule.Producer.Api.Dto;

public sealed class DelayTripRequest
{
    [Range(1, int.MaxValue)]
    public required int DelayMinutes { get; init; }

    public string Reason { get; init; } = string.Empty;
}
