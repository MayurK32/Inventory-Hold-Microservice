using InventoryHold.Domain.Cache;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Repositories;

namespace InventoryHold.WebApi.Services;

public sealed class InventoryService(
    IInventoryRepository inventoryRepository,
    IHoldRepository holdRepository,
    IInventoryCache cache)
{
    public async Task<IReadOnlyList<InventoryItem>> GetInventoryAsync(CancellationToken ct = default)
    {
        var cached = await cache.GetInventoryAsync(ct);
        if (cached is not null) return cached;

        var items = await inventoryRepository.GetAllAsync(ct);
        await cache.SetInventoryAsync(items, ct);
        return items;
    }

    public async Task<IReadOnlyList<InventoryItem>> ResetInventoryAsync(CancellationToken ct = default)
    {
        await holdRepository.DeleteAllAsync(ct);
        await inventoryRepository.ResetAllAsync(ct);
        await cache.FlushAllAsync(ct);
        return await inventoryRepository.GetAllAsync(ct);
    }
}
