using InventoryHold.Domain.Cache;
using InventoryHold.Domain.Entities;

namespace InventoryHold.WebApi.Stubs;

internal sealed class NullInventoryCache : IInventoryCache
{
    public Task<IReadOnlyList<InventoryItem>?> GetInventoryAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<InventoryItem>?>(null);
    public Task SetInventoryAsync(IReadOnlyList<InventoryItem> items, CancellationToken ct = default) => Task.CompletedTask;
    public Task InvalidateInventoryAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task<Hold?> GetHoldAsync(string holdId, CancellationToken ct = default) => Task.FromResult<Hold?>(null);
    public Task SetHoldAsync(Hold hold, CancellationToken ct = default) => Task.CompletedTask;
    public Task InvalidateHoldAsync(string holdId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<int?> GetExpirationMinutesAsync(CancellationToken ct = default) => Task.FromResult<int?>(null);
    public Task SetExpirationMinutesAsync(int minutes, CancellationToken ct = default) => Task.CompletedTask;
    public Task FlushAllAsync(CancellationToken ct = default) => Task.CompletedTask;
}
