using InventoryHold.Domain.Entities;

namespace InventoryHold.Domain.Cache;

public interface IInventoryCache
{
    Task<IReadOnlyList<InventoryItem>?> GetInventoryAsync(CancellationToken ct = default);
    Task SetInventoryAsync(IReadOnlyList<InventoryItem> items, CancellationToken ct = default);
    Task InvalidateInventoryAsync(CancellationToken ct = default);

    Task<Hold?> GetHoldAsync(string holdId, CancellationToken ct = default);
    Task SetHoldAsync(Hold hold, CancellationToken ct = default);
    Task InvalidateHoldAsync(string holdId, CancellationToken ct = default);

    Task<int?> GetExpirationMinutesAsync(CancellationToken ct = default);
    Task SetExpirationMinutesAsync(int minutes, CancellationToken ct = default);

    Task FlushAllAsync(CancellationToken ct = default);
}
