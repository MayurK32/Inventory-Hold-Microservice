using System.Text.Json;
using InventoryHold.Contracts.Events;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Messaging;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace InventoryHold.Infrastructure.Messaging;

public sealed class RabbitMqHoldEventPublisher(
    IConnection connection,
    ILogger<RabbitMqHoldEventPublisher> logger) : IHoldEventPublisher, IAsyncDisposable
{
    private const string Exchange = "inventory.hold.events";
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IChannel? _channel;

    public Task PublishHoldCreatedAsync(Hold hold, CancellationToken ct = default) =>
        PublishAsync("hold.created", new HoldCreatedEvent(
            hold.Id, hold.CustomerName, hold.Status.ToString(),
            hold.Items.Select(i => new EventItem(i.ProductId, i.ProductName, i.Quantity)).ToList(),
            hold.CreatedAt, hold.ExpiresAt), ct);

    public Task PublishHoldReleasedAsync(Hold hold, CancellationToken ct = default) =>
        PublishAsync("hold.released", new HoldReleasedEvent(
            hold.Id, hold.CustomerName, hold.Status.ToString(),
            hold.Items.Select(i => new EventItem(i.ProductId, i.ProductName, i.Quantity)).ToList(),
            hold.ReleasedAt!.Value), ct);

    public Task PublishHoldExpiredAsync(Hold hold, CancellationToken ct = default) =>
        PublishAsync("hold.expired", new HoldExpiredEvent(
            hold.Id, hold.CustomerName, hold.Status.ToString(),
            hold.Items.Select(i => new EventItem(i.ProductId, i.ProductName, i.Quantity)).ToList(),
            hold.ExpiredAt!.Value), ct);

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }
        _channel = await connection.CreateChannelAsync(cancellationToken: ct);
        return _channel;
    }

    private async Task PublishAsync(string routingKey, object evt, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var channel = await GetChannelAsync(ct);
            var body = JsonSerializer.SerializeToUtf8Bytes(evt, JsonOpts);
            await channel.BasicPublishAsync(Exchange, routingKey, false, new BasicProperties(), body, ct);
        }
        catch (Exception ex)
        {
            _channel = null;
            logger.LogError(ex, "Failed to publish {RoutingKey}", routingKey);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
            _channel = null;
        }
        _lock.Dispose();
    }
}
