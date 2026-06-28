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
    private static readonly SemaphoreSlim _fetchLock = new(1, 1);

    public async Task<IReadOnlyList<InventoryItem>> GetInventoryAsync(CancellationToken ct = default)
    {
        var cached = await cache.GetInventoryAsync(ct);
        if (cached is not null)
        {
            logger.LogDebug("Inventory cache hit ({Count} items)", cached.Count);
            return cached;
        }

        await _fetchLock.WaitAsync(ct);
        try
        {
            var cached2 = await cache.GetInventoryAsync(ct);
            if (cached2 is not null)
            {
                logger.LogDebug("Inventory cache hit (post-lock, {Count} items)", cached2.Count);
                return cached2;
            }

            logger.LogDebug("Inventory cache miss, fetching from DB");
            var items = await inventoryRepository.GetAllAsync(ct);
            await cache.SetInventoryAsync(items, ct);
            return items;
        }
        finally
        {
            _fetchLock.Release();
        }
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
