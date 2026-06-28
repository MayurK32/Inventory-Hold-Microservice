using FluentAssertions;
using InventoryHold.Domain.Cache;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Repositories;
using InventoryHold.WebApi.Services;
using Moq;

namespace InventoryHold.UnitTests.Application;

public class ResetInventoryServiceTests
{
    private readonly Mock<IInventoryRepository> _inventory = new();
    private readonly Mock<IHoldRepository>      _holds     = new();
    private readonly Mock<IInventoryCache>      _cache     = new();
    private readonly InventoryService _service;

    public ResetInventoryServiceTests()
    {
        _service = new InventoryService(_inventory.Object, _holds.Object, _cache.Object);
    }

    private static IReadOnlyList<InventoryItem> FreshItems() =>
        [new InventoryItem { ProductId = "widget-a", Name = "Widget A", TotalQuantity = 50, AvailableQuantity = 50 }];

    [Fact]
    public async Task ResetInventoryAsync_DeletesAllHolds_ResetsInventory_FlushesCache()
    {
        _inventory.Setup(r => r.GetAllAsync(default)).ReturnsAsync(FreshItems());

        await _service.ResetInventoryAsync();

        _holds.Verify(r => r.DeleteAllAsync(default), Times.Once);
        _inventory.Verify(r => r.ResetAllAsync(default), Times.Once);
        _cache.Verify(c => c.FlushAllAsync(default), Times.Once);
    }

    [Fact]
    public async Task ResetInventoryAsync_ReturnsFreshInventoryFromDb()
    {
        var fresh = FreshItems();
        _inventory.Setup(r => r.GetAllAsync(default)).ReturnsAsync(fresh);

        var result = await _service.ResetInventoryAsync();

        result.Should().BeEquivalentTo(fresh);
        _inventory.Verify(r => r.GetAllAsync(default), Times.Once);
    }

    [Fact]
    public async Task ResetInventoryAsync_DoesNotReadCacheAfterFlush()
    {
        _inventory.Setup(r => r.GetAllAsync(default)).ReturnsAsync(FreshItems());

        await _service.ResetInventoryAsync();

        _cache.Verify(c => c.GetInventoryAsync(default), Times.Never);
    }
}
