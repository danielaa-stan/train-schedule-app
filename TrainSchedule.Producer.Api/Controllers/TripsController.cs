using Microsoft.AspNetCore.Mvc;
using TrainSchedule.Producer.Api.Domain;
using TrainSchedule.Producer.Api.Dto;
using TrainSchedule.Producer.Api.Services;

namespace TrainSchedule.Producer.Api.Controllers;

[ApiController]
[Route("api/trips")]
public sealed class TripsController(
    InMemoryEventStore eventStore,
    RabbitMqPublisher publisher,
    ILogger<TripsController> logger) : ControllerBase
{
    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateTripRequest request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        return ExecuteAsync(id, trip => trip.Create(id, request.TrainNumber,
            request.DepartureStation, request.ArrivalStation,
            request.DepartureTime, request.ArrivalTime, request.Platform),
            cancellationToken, creating: true);
    }

    [HttpPost("{id:guid}/delay")]
    public Task<IActionResult> Delay(Guid id, [FromBody] DelayTripRequest request, CancellationToken cancellationToken)
        => ExecuteAsync(id, trip => trip.Delay(request.DelayMinutes, request.Reason), cancellationToken);

    [HttpPost("{id:guid}/platform")]
    public Task<IActionResult> ChangePlatform(Guid id, [FromBody] ChangePlatformRequest request, CancellationToken cancellationToken)
        => ExecuteAsync(id, trip => trip.ChangePlatform(request.NewPlatform), cancellationToken);

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, [FromBody] CancelTripRequest request, CancellationToken cancellationToken)
        => ExecuteAsync(id, trip => trip.Cancel(request.Reason), cancellationToken);

    [HttpPost("{id:guid}/restore")]
    public Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
        => ExecuteAsync(id, trip => trip.Restore(), cancellationToken);

    [HttpPost("{id:guid}/complete")]
    public Task<IActionResult> Complete(Guid id, [FromBody] CompleteTripRequest request, CancellationToken cancellationToken)
        => ExecuteAsync(id, trip => trip.Complete(request.ActualArrivalTime), cancellationToken);

    private async Task<IActionResult> ExecuteAsync(Guid id, Action<TripAggregate> command,
        CancellationToken cancellationToken, bool creating = false)
    {
        var history = eventStore.GetEvents(id);
        if (!creating && history.Count == 0)
        {
            return NotFound(new { TripId = id, Message = "Trip does not exist." });
        }

        var trip = new TripAggregate(history);
        try
        {
            command(trip);
            eventStore.AppendEvents(id, trip.UncommittedEvents, history.Count);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { TripId = id, Message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { TripId = id, Message = exception.Message });
        }

        var savedEvents = trip.UncommittedEvents.ToArray();
        trip.ClearUncommittedEvents();

        try
        {
            foreach (var @event in savedEvents)
            {
                await publisher.PublishAsync(@event, cancellationToken);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Trip {TripId} events were saved but publication was not confirmed.", id);

            // This lab uses an in-memory store, without an outbox or automatic retries.
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                TripId = id,
                EventsSaved = true,
                PublicationConfirmed = false,
                Message = "The command was saved, but RabbitMQ publication was not confirmed. Do not repeat the command automatically."
            });
        }

        var response = new
        {
            TripId = id,
            Status = trip.Status.ToString(),
            Version = history.Count + savedEvents.Length,
            PublishedEvents = savedEvents.Select(e => e.GetType().Name).ToArray()
        };

        return creating ? StatusCode(StatusCodes.Status201Created, response) : Ok(response);
    }
}
