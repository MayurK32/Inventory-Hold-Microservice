using FluentAssertions;
using InventoryHold.Contracts.Settings;
using InventoryHold.Domain.Cache;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Exceptions;
using InventoryHold.Domain.Repositories;
using InventoryHold.Domain.Transactions;
using InventoryHold.WebApi.Services;
using Microsoft.Extensions.Options;
using Moq;

namespace InventoryHold.UnitTests.Application;

public class ReleaseHoldServiceTests
{
    private readonly Mock<IHoldRepository>      _holds     = new();
    private readonly Mock<IInventoryRepository> _inventory = new();
    private readonly Mock<IInventoryCache>      _cache     = new();
    private readonly HoldService _service;

    public ReleaseHoldServiceTests()
    {
        _service = new HoldService(
            _holds.Object, _inventory.Object,
            Mock.Of<ISettingsRepository>(), Mock.Of<ITransactionFactory>(),
            Options.Create(new HoldSettings()), _cache.Object);
    }

    private static Hold MakeHold(string id, HoldStatus status,
        DateTime? releasedAt = null, DateTime? expiredAt = null) =>
        Hold.Reconstruct(id, null, status,
            [new HoldItem("widget-a", "Widget A", 3)],
            DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(5),
            releasedAt, expiredAt);

    [Fact]
    public async Task ReleaseHoldAsync_ActiveHold_ReturnsReleasedHoldRestoresInventoryAndInvalidatesCaches()
    {
        var releasedHold = MakeHold("h1", HoldStatus.Released, DateTime.UtcNow);

        _holds.Setup(r => r.AtomicTransitionAsync("h1", HoldStatus.Active, HoldStatus.Released, It.IsAny<DateTime>(), default))
              .ReturnsAsync(releasedHold);

        var result = await _service.ReleaseHoldAsync("h1");

        result.Status.Should().Be(HoldStatus.Released);
        result.ReleasedAt.Should().NotBeNull();
        _inventory.Verify(r => r.IncrementAsync(releasedHold.Items, default), Times.Once);
        _cache.Verify(c => c.InvalidateInventoryAsync(default), Times.Once);
        _cache.Verify(c => c.InvalidateHoldAsync("h1", default), Times.Once);
    }

    [Fact]
    public async Task ReleaseHoldAsync_HoldNotFound_ThrowsHoldNotFoundException()
    {
        _holds.Setup(r => r.AtomicTransitionAsync("x", HoldStatus.Active, HoldStatus.Released, It.IsAny<DateTime>(), default))
              .ReturnsAsync((Hold?)null);
        _holds.Setup(r => r.GetByIdAsync("x", default)).ReturnsAsync((Hold?)null);

        await FluentActions.Invoking(() => _service.ReleaseHoldAsync("x"))
            .Should().ThrowAsync<HoldNotFoundException>();
    }

    [Fact]
    public async Task ReleaseHoldAsync_AlreadyReleased_ThrowsHoldTerminatedException()
    {
        var releasedAt = DateTime.UtcNow.AddMinutes(-2);
        var hold = MakeHold("h1", HoldStatus.Released, releasedAt);

        _holds.Setup(r => r.AtomicTransitionAsync("h1", HoldStatus.Active, HoldStatus.Released, It.IsAny<DateTime>(), default))
              .ReturnsAsync((Hold?)null);
        _holds.Setup(r => r.GetByIdAsync("h1", default)).ReturnsAsync(hold);

        var ex = await FluentActions.Invoking(() => _service.ReleaseHoldAsync("h1"))
            .Should().ThrowAsync<HoldTerminatedException>();
        ex.Which.At.Should().Be(releasedAt);
    }

    [Fact]
    public async Task ReleaseHoldAsync_AlreadyExpired_ThrowsHoldTerminatedException()
    {
        var expiredAt = DateTime.UtcNow.AddMinutes(-1);
        var hold = MakeHold("h1", HoldStatus.Expired, null, expiredAt);

        _holds.Setup(r => r.AtomicTransitionAsync("h1", HoldStatus.Active, HoldStatus.Released, It.IsAny<DateTime>(), default))
              .ReturnsAsync((Hold?)null);
        _holds.Setup(r => r.GetByIdAsync("h1", default)).ReturnsAsync(hold);

        var ex = await FluentActions.Invoking(() => _service.ReleaseHoldAsync("h1"))
            .Should().ThrowAsync<HoldTerminatedException>();
        ex.Which.At.Should().Be(expiredAt);
    }
}
