using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using TrainSchedule.Producer.Api.Events;

namespace TrainSchedule.Producer.Api.Services;

public sealed class RabbitMqPublisher(IOptions<RabbitMqOptions> options)
    : IHostedService, IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };

        try
        {
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(
                new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true), cancellationToken);

            await _channel.ExchangeDeclareAsync(
                exchange: _options.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task PublishAsync(IEvent @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var routingKey = @event switch
        {
            TripCreated => "trip.created",
            TripDelayed => "trip.delayed",
            TripPlatformChanged => "trip.platform_changed",
            TripCancelled => "trip.cancelled",
            TripRestored => "trip.restored",
            TripCompleted => "trip.completed",
            _ => throw new ArgumentException("Unsupported trip event type.", nameof(@event))
        };

        // Serialize the concrete type so event-specific fields are included.
        var body = JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType());
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true,
            MessageId = @event.EventId.ToString(),
            Type = @event.GetType().Name,
            Headers = new Dictionary<string, object?>
            {
                ["EventType"] = @event.GetType().Name
            }
        };

        // HTTP requests can run concurrently; a publishing channel must be serialized.
        await _channelLock.WaitAsync(cancellationToken);
        try
        {
            var channel = _channel
                ?? throw new InvalidOperationException("RabbitMQ publisher has not been started.");

            // Mandatory + confirms reports unroutable messages instead of silently dropping them.
            await channel.BasicPublishAsync(
                exchange: _options.ExchangeName,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => DisposeAsync().AsTask();

    public async ValueTask DisposeAsync()
    {
        await _channelLock.WaitAsync();
        try
        {
            if (_channel is not null)
            {
                await _channel.DisposeAsync();
                _channel = null;
            }

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
        finally
        {
            _channelLock.Release();
        }
    }
}
