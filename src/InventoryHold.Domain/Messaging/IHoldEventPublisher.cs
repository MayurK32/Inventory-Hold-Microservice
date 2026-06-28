using InventoryHold.Domain.Entities;

namespace InventoryHold.Domain.Messaging;

public interface IHoldEventPublisher
{
    Task PublishHoldCreatedAsync(Hold hold, CancellationToken ct = default);
    Task PublishHoldReleasedAsync(Hold hold, CancellationToken ct = default);
    Task PublishHoldExpiredAsync(Hold hold, CancellationToken ct = default);
}
