using InventoryHold.Domain.Cache;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Repositories;

namespace InventoryHold.WebApi.Services;

public sealed class InventoryService(
    IInventoryRepository inventoryRepository,
    IHoldRepository holdRepository,
    IInventoryCache cache,
    ILogger<InventoryService> logger)
{
    public async Task<IReadOnlyList<InventoryItem>> GetInventoryAsync(CancellationToken ct = default)
    {
        var cached = await cache.GetInventoryAsync(ct);
        if (cached is not null)
        {
            logger.LogDebug("Inventory cache hit ({Count} items)", cached.Count);
            return cached;
        }

        logger.LogDebug("Inventory cache miss, fetching from DB");
        var items = await inventoryRepository.GetAllAsync(ct);
        await cache.SetInventoryAsync(items, ct);
        return items;
    }

    public async Task<IReadOnlyList<InventoryItem>> ResetInventoryAsync(CancellationToken ct = default)
    {
        logger.LogWarning("Inventory reset requested — deleting all holds, restoring seed quantities");
        await holdRepository.DeleteAllAsync(ct);
        await inventoryRepository.ResetAllAsync(ct);
        await cache.FlushAllAsync(ct);
        logger.LogInformation("Inventory reset complete");
        return await inventoryRepository.GetAllAsync(ct);
    }
}
