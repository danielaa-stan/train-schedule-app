using System.ComponentModel.DataAnnotations;

namespace TrainSchedule.Producer.Api.Dto;

public sealed class CreateTripRequest
{
    [Required]
    public required string TrainNumber { get; init; }

    [Required]
    public required string DepartureStation { get; init; }

    [Required]
    public required string ArrivalStation { get; init; }

    public required DateTimeOffset DepartureTime { get; init; }

    public required DateTimeOffset ArrivalTime { get; init; }

    [Required]
    public required string Platform { get; init; }
}
