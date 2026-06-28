using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Messaging;

namespace InventoryHold.WebApi.Stubs;

internal sealed class NullHoldEventPublisher : IHoldEventPublisher
{
    public Task PublishHoldCreatedAsync(Hold hold, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishHoldReleasedAsync(Hold hold, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishHoldExpiredAsync(Hold hold, CancellationToken ct = default) => Task.CompletedTask;
}
