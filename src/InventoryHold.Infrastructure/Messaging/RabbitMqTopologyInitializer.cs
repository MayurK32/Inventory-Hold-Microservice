using RabbitMQ.Client;

namespace InventoryHold.Infrastructure.Messaging;

public sealed class RabbitMqTopologyInitializer(IConnection connection)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.ExchangeDeclareAsync(
            "inventory.hold.events", ExchangeType.Direct, durable: true, cancellationToken: ct);

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
        }
    }
}
