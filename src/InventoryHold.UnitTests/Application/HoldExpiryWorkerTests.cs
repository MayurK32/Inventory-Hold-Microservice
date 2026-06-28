using FluentAssertions;
using InventoryHold.Contracts.Settings;
using InventoryHold.Domain.Cache;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Messaging;
using InventoryHold.Domain.Repositories;
using InventoryHold.WebApi.Workers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InventoryHold.UnitTests.Application;

public class HoldExpiryWorkerTests
{
    private readonly Mock<IHoldRepository>      _holds     = new();
    private readonly Mock<IInventoryRepository> _inventory = new();
    private readonly Mock<IInventoryCache>      _cache     = new();
    private readonly Mock<IHoldEventPublisher>  _events    = new();
    private readonly HoldExpiryWorker _worker;

    public HoldExpiryWorkerTests()
    {
        _worker = new HoldExpiryWorker(
            _holds.Object, _inventory.Object, _cache.Object, _events.Object,
            Options.Create(new HoldSettings { ExpirationMinutes = 15, PollingIntervalSeconds = 30 }),
            Mock.Of<ILogger<HoldExpiryWorker>>());
    }

    private static Hold MakeExpiredHold(string id, string productId, int qty) =>
        Hold.Reconstruct(id, null, HoldStatus.Active,
            [new HoldItem(productId, "Widget", qty)],
            DateTime.UtcNow.AddMinutes(-20),
            DateTime.UtcNow.AddMinutes(-5),
            null, null);

    [Fact]
    public async Task ProcessExpiredHoldsAsync_NoExpiredHolds_ZeroDbAndCacheOps()
    {
        _holds.Setup(r => r.GetExpiredActiveAsync(It.IsAny<DateTime>(), default))
              .ReturnsAsync(Array.Empty<Hold>());

        await _worker.ProcessExpiredHoldsAsync(default);

        _holds.Verify(r => r.AtomicTransitionAsync(
            It.IsAny<string>(), It.IsAny<HoldStatus>(), It.IsAny<HoldStatus>(),
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
        _inventory.Verify(r => r.IncrementAsync(
            It.IsAny<IReadOnlyList<HoldItem>>(), It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.InvalidateInventoryAsync(default), Times.Never);
    }

    [Fact]
    public async Task ProcessExpiredHoldsAsync_TwoExpiredHolds_TransitionsBothAndRestoresInventory()
    {
        var hold1 = MakeExpiredHold("h1", "widget-a", 3);
        var hold2 = MakeExpiredHold("h2", "widget-b", 5);

        _holds.Setup(r => r.GetExpiredActiveAsync(It.IsAny<DateTime>(), default))
              .ReturnsAsync([hold1, hold2]);
        _holds.Setup(r => r.AtomicTransitionAsync("h1", HoldStatus.Active, HoldStatus.Expired, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(hold1);
        _holds.Setup(r => r.AtomicTransitionAsync("h2", HoldStatus.Active, HoldStatus.Expired, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(hold2);

        await _worker.ProcessExpiredHoldsAsync(default);

        _holds.Verify(r => r.AtomicTransitionAsync(
            It.IsAny<string>(), HoldStatus.Active, HoldStatus.Expired,
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _inventory.Verify(r => r.IncrementAsync(hold1.Items, It.IsAny<CancellationToken>()), Times.Once);
        _inventory.Verify(r => r.IncrementAsync(hold2.Items, It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.InvalidateInventoryAsync(default), Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredHoldsAsync_RaceCondition_SkipsInventoryAndEvent()
    {
        var hold = MakeExpiredHold("h1", "widget-a", 3);

        _holds.Setup(r => r.GetExpiredActiveAsync(It.IsAny<DateTime>(), default))
              .ReturnsAsync([hold]);
        _holds.Setup(r => r.AtomicTransitionAsync("h1", HoldStatus.Active, HoldStatus.Expired, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((Hold?)null);

        await _worker.ProcessExpiredHoldsAsync(default);

        _inventory.Verify(r => r.IncrementAsync(
            It.IsAny<IReadOnlyList<HoldItem>>(), It.IsAny<CancellationToken>()), Times.Never);
        _events.Verify(e => e.PublishHoldExpiredAsync(
            It.IsAny<Hold>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessExpiredHoldsAsync_AllTransitionsLostRace_NoCacheInvalidation()
    {
        var hold = MakeExpiredHold("h1", "widget-a", 3);

        _holds.Setup(r => r.GetExpiredActiveAsync(It.IsAny<DateTime>(), default))
              .ReturnsAsync([hold]);
        _holds.Setup(r => r.AtomicTransitionAsync("h1", HoldStatus.Active, HoldStatus.Expired, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((Hold?)null);

        await _worker.ProcessExpiredHoldsAsync(default);

        _cache.Verify(c => c.InvalidateInventoryAsync(default), Times.Never);
    }

    [Fact]
    public async Task ProcessExpiredHoldsAsync_AlreadyRunning_SecondCallSkips()
    {
        var tcs = new TaskCompletionSource<IReadOnlyList<Hold>>();
        _holds.Setup(r => r.GetExpiredActiveAsync(It.IsAny<DateTime>(), default))
              .Returns(tcs.Task);

        var first = _worker.ProcessExpiredHoldsAsync(default);  // acquires lock, blocks at GetExpiredActiveAsync
        var second = _worker.ProcessExpiredHoldsAsync(default); // lock taken → skips immediately

        await second; // completes right away
        tcs.SetResult(Array.Empty<Hold>());
        await first;  // now completes

        _holds.Verify(r => r.GetExpiredActiveAsync(It.IsAny<DateTime>(), default), Times.Once);
    }
}
