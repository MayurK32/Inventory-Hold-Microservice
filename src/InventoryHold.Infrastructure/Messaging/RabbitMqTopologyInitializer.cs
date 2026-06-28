using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace InventoryHold.Infrastructure.Messaging;

public sealed class RabbitMqTopologyInitializer(IConnection connection, ILogger<RabbitMqTopologyInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        logger.LogInformation("RabbitMQ topology init started. Endpoint: {Endpoint}", connection.Endpoint);
        try
        {
            await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
            logger.LogInformation("Channel created.");

            await channel.ExchangeDeclareAsync(
                "inventory.hold.events", ExchangeType.Direct, durable: true, cancellationToken: ct);
            logger.LogInformation("Exchange 'inventory.hold.events' declared.");

            foreach (var (queue, key) in new[]
            {
                ("hold.created.queue",  "hold.created"),
                ("hold.released.queue", "hold.released"),
                ("hold.expired.queue",  "hold.expired")
            })
            {
                await channel.QueueDeclareAsync(queue, durable: true, exclusive: false,
                    autoDelete: false, cancellationToken: ct);
                await channel.QueueBindAsync(queue, "inventory.hold.events", key, cancellationToken: ct);
                logger.LogInformation("Queue '{Queue}' declared and bound.", queue);
            }

            logger.LogInformation("RabbitMQ topology init complete.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RabbitMQ topology init FAILED.");
            throw;
        }
    }
}
