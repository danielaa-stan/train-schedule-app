using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using TrainSchedule.Board.Consumer;

Console.OutputEncoding = Encoding.UTF8;
using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    shutdown.Cancel();
};

using var settings = JsonDocument.Parse(
    await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
var config = settings.RootElement.GetProperty("RabbitMq");
string Setting(string key) => Environment.GetEnvironmentVariable($"RabbitMq__{key}")
    ?? config.GetProperty(key).ToString();

var exchange = Setting("ExchangeName");
var boardQueue = Setting("BoardQueue");
var alertsQueue = Setting("AlertsQueue");
var factory = new ConnectionFactory
{
    HostName = Setting("HostName"), Port = int.Parse(Setting("Port")),
    UserName = Setting("UserName"), Password = Setting("Password"),
    VirtualHost = Setting("VirtualHost"), ConsumerDispatchConcurrency = 1
};

await using var connection = await factory.CreateConnectionAsync(shutdown.Token);
await using var boardChannel = await connection.CreateChannelAsync(cancellationToken: shutdown.Token);
await using var alertsChannel = await connection.CreateChannelAsync(cancellationToken: shutdown.Token);
await boardChannel.ExchangeDeclareAsync(exchange, ExchangeType.Topic, durable: true, autoDelete: false,
    cancellationToken: shutdown.Token);

var projection = new TripProjection();
await SubscribeAsync(boardChannel, boardQueue, "trip.#", (type, data) =>
{
    var view = projection.Apply(type, data);
    if (view is not null) Console.WriteLine(view);
});

// A separate queue gets its own copy of cancellations through a wildcard binding.
await SubscribeAsync(alertsChannel, alertsQueue, "*.cancelled", (type, data) =>
{
    if (type != "TripCancelled") throw new JsonException("Expected TripCancelled in the alerts queue.");
    Console.WriteLine($"[ALERT] Trip {data.GetProperty("TripId").GetGuid()} cancelled: {data.GetProperty("Reason").GetString()}");
});

Console.WriteLine($"Listening: {boardQueue} (trip.#), {alertsQueue} (*.cancelled). Press Ctrl+C to stop.");
Console.WriteLine("Read model is in memory; use a fresh event stream after restarting the demo.");
try
{
    await Task.Delay(Timeout.Infinite, shutdown.Token);
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }

async Task SubscribeAsync(IChannel channel, string queue, string binding, Action<string, JsonElement> handle)
{
    await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
        cancellationToken: shutdown.Token);
    await channel.QueueBindAsync(queue, exchange, binding, cancellationToken: shutdown.Token);
    await channel.BasicQosAsync(0, 1, false, shutdown.Token);
    var consumer = new AsyncEventingBasicConsumer(channel);
    consumer.ReceivedAsync += async (_, delivery) =>
    {
        try
        {
            var type = delivery.BasicProperties.Headers?.TryGetValue("EventType", out var value) == true
                && value is byte[] bytes ? Encoding.UTF8.GetString(bytes)
                : throw new JsonException("Missing EventType header.");
            using var document = JsonDocument.Parse(delivery.Body);
            handle(type, document.RootElement);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException
            or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {
            Console.Error.WriteLine($"[REJECTED] {queue}: {exception.Message}");
            // Invalid messages are discarded; this lab has no dead-letter queue.
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false);
    };

    await channel.BasicConsumeAsync(queue, autoAck: false, consumer: consumer, cancellationToken: shutdown.Token);
}
