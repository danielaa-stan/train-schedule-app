using System.ComponentModel.DataAnnotations;

namespace TrainSchedule.Producer.Api.Dto;

public sealed class ChangePlatformRequest
{
    [Required]
    public required string NewPlatform { get; init; }
}
