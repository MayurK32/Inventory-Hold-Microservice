using InventoryHold.Contracts.Settings;
using InventoryHold.Domain.Cache;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Messaging;
using InventoryHold.Domain.Repositories;
using Microsoft.Extensions.Options;

namespace InventoryHold.WebApi.Workers;

public sealed class HoldExpiryWorker(
    IHoldRepository holdRepository,
    IInventoryRepository inventoryRepository,
    IInventoryCache cache,
    IHoldEventPublisher eventPublisher,
    IOptions<HoldSettings> holdSettings,
    ILogger<HoldExpiryWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(
                TimeSpan.FromSeconds(holdSettings.Value.PollingIntervalSeconds),
                stoppingToken);
            try
            {
                await ProcessExpiredHoldsAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error processing expired holds");
            }
        }
    }

    public async Task ProcessExpiredHoldsAsync(CancellationToken ct)
    {
        var expired = await holdRepository.GetExpiredActiveAsync(DateTime.UtcNow, ct);

        logger.LogDebug("Expiry tick: {Count} candidate(s) found", expired.Count);

        if (expired.Count == 0) return;

        var transitioned = 0;

        foreach (var hold in expired)
        {
            var result = await holdRepository.AtomicTransitionAsync(
                hold.Id, HoldStatus.Active, HoldStatus.Expired, DateTime.UtcNow, ct);

            if (result is null)
            {
                logger.LogDebug("Hold {HoldId} already transitioned by another operation — skipping", hold.Id);
                continue;
            }

            transitioned++;
            logger.LogInformation("Hold {HoldId} expired — restoring {ItemCount} item(s)", result.Id, result.Items.Count);
            await inventoryRepository.IncrementAsync(hold.Items, ct);
            await eventPublisher.PublishHoldExpiredAsync(result, ct);
        }

        if (transitioned > 0)
        {
            logger.LogInformation("Expiry tick complete: {Transitioned}/{Total} hold(s) expired", transitioned, expired.Count);
            await cache.InvalidateInventoryAsync(ct);
        }
    }
}
