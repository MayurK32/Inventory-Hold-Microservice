using InventoryHold.Contracts.Requests;
using InventoryHold.Contracts.Settings;
using InventoryHold.Domain.Cache;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Exceptions;
using InventoryHold.Domain.Messaging;
using InventoryHold.Domain.Repositories;
using InventoryHold.Domain.Transactions;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace InventoryHold.WebApi.Services;

public sealed class HoldService(
    IHoldRepository holdRepository,
    IInventoryRepository inventoryRepository,
    ISettingsRepository settingsRepository,
    ITransactionFactory transactionFactory,
    IOptions<HoldSettings> holdSettings,
    IInventoryCache cache,
    IHoldEventPublisher eventPublisher,
    ILogger<HoldService> logger)
{
    public async Task<Hold> CreateHoldAsync(CreateHoldRequest request, CancellationToken ct = default)
    {
        if (request.Items is null || request.Items.Count == 0)
            throw new DomainException("Hold must have at least one item.");

        foreach (var item in request.Items)
            if (item.Quantity <= 0)
                throw new DomainException($"Quantity for '{item.ProductId}' must be at least 1.");

        if (request.Items.Count > 50)
            throw new DomainException("Hold cannot contain more than 50 items.");

        logger.LogInformation("Creating hold for {CustomerName} with {ItemCount} items",
            request.CustomerName, request.Items.Count);

        var cachedExpiry = await cache.GetExpirationMinutesAsync(ct);
        int expirationMinutes;
        if (cachedExpiry.HasValue)
        {
            expirationMinutes = cachedExpiry.Value;
        }
        else
        {
            expirationMinutes = await settingsRepository
                .GetExpirationMinutesAsync(holdSettings.Value.ExpirationMinutes, ct);
            await cache.SetExpirationMinutesAsync(expirationMinutes, ct);
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var created = await AttemptCreateAsync(request, expirationMinutes, ct);
                logger.LogInformation("Hold {HoldId} created, expires {ExpiresAt} (attempt {Attempt})",
                    created.Id, created.ExpiresAt, attempt + 1);
                await cache.InvalidateInventoryAsync(ct);
                try { await eventPublisher.PublishHoldCreatedAsync(created, ct); }
                catch (Exception ex) { logger.LogError(ex, "Failed to publish HoldCreated for {HoldId}", created.Id); }
                return created;
            }
            catch (MongoCommandException e) when (IsWriteConflict(e))
            {
                if (attempt == 2) throw new StockUnavailableException();
                logger.LogWarning("Write conflict on hold create, retrying (attempt {Attempt})", attempt + 1);
                await Task.Delay(50, ct);
            }
        }

        throw new StockUnavailableException();
    }

    public async Task<Hold> GetHoldAsync(string holdId, CancellationToken ct = default)
    {
        var cached = await cache.GetHoldAsync(holdId, ct);
        if (cached is not null)
        {
            logger.LogDebug("Hold {HoldId} cache hit", holdId);
            return cached;
        }

        logger.LogDebug("Hold {HoldId} cache miss, fetching from DB", holdId);
        var hold = await holdRepository.GetByIdAsync(holdId, ct)
            ?? throw new HoldNotFoundException(holdId);

        await cache.SetHoldAsync(hold, ct);
        return hold;
    }

    public async Task<Hold> ReleaseHoldAsync(string holdId, CancellationToken ct = default)
    {
        logger.LogInformation("Releasing hold {HoldId}", holdId);

        var result = await holdRepository.AtomicTransitionAsync(
            holdId, HoldStatus.Active, HoldStatus.Released, DateTime.UtcNow, ct);

        if (result is null)
        {
            var hold = await holdRepository.GetByIdAsync(holdId, ct)
                ?? throw new HoldNotFoundException(holdId);
            hold.MarkReleased(); // throws HoldTerminatedException with correct At timestamp
            throw new InvalidOperationException("unreachable");
        }

        await inventoryRepository.IncrementAsync(result.Items, ct);
        logger.LogInformation("Hold {HoldId} released, inventory restored", holdId);
        await cache.InvalidateInventoryAsync(ct);
        await cache.InvalidateHoldAsync(holdId, ct);
        try { await eventPublisher.PublishHoldReleasedAsync(result, ct); }
        catch (Exception ex) { logger.LogError(ex, "Failed to publish HoldReleased for {HoldId}", holdId); }
        return result;
    }

    public async Task<(IReadOnlyList<Hold> Items, long Total)> ListHoldsAsync(
        string? status, int page, int pageSize, CancellationToken ct = default)
    {
        if (pageSize > 100) throw new DomainException("pageSize cannot exceed 100.");
        if (pageSize < 1)   throw new DomainException("pageSize must be at least 1.");
        return await holdRepository.GetPagedAsync(status, page, pageSize, ct);
    }

    public async Task<(IReadOnlyList<Hold> Items, string? NextCursor)> ListHoldsByCursorAsync(
        string? status, string? cursor, int pageSize, CancellationToken ct = default)
    {
        if (pageSize > 100) throw new DomainException("pageSize cannot exceed 100.");
        if (pageSize < 1)   throw new DomainException("pageSize must be at least 1.");
        return await holdRepository.GetPagedByCursorAsync(status, cursor, pageSize, ct);
    }

    // Message check first: if e.Code throws internally, the filter would silently evaluate to false.
    // Code 112 = WriteConflict; message check covers Driver 3.x where Code may be inaccessible.
    private static bool IsWriteConflict(MongoCommandException e) =>
        e.Message.Contains("WriteConflict", StringComparison.OrdinalIgnoreCase) || e.Code == 112;

    private async Task<Hold> AttemptCreateAsync(
        CreateHoldRequest request, int expirationMinutes, CancellationToken ct)
    {
        await using var tx = await transactionFactory.BeginAsync(ct);
        try
        {
            var productIds = request.Items.Select(i => i.ProductId).ToList();
            var inventoryMap = (await inventoryRepository.GetByProductIdsAsync(productIds, ct))
                .ToDictionary(i => i.ProductId);

            var failures = new List<StockFailure>();
            var holdItems = new List<HoldItem>();

            foreach (var item in request.Items)
            {
                if (!inventoryMap.TryGetValue(item.ProductId, out var inv))
                    throw new ProductNotFoundException(item.ProductId);

                if (inv.AvailableQuantity < item.Quantity)
                    failures.Add(new StockFailure(item.ProductId, item.Quantity, inv.AvailableQuantity));
                else
                    holdItems.Add(new HoldItem(item.ProductId, inv.Name, item.Quantity));
            }

            if (failures.Count > 0) throw new InsufficientStockException(failures);

            var hold = Hold.Create(request.CustomerName, holdItems, expirationMinutes);
            await inventoryRepository.DecrementBatchAsync(holdItems, tx, ct);
            var inserted = await holdRepository.InsertAsync(hold, tx, ct);

            await tx.CommitAsync(ct);
            return inserted;
        }
        catch
        {
            await tx.AbortAsync(ct);
            throw;
        }
    }
}
