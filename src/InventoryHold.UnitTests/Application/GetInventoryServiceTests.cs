using FluentAssertions;
using InventoryHold.Domain.Cache;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Repositories;
using InventoryHold.WebApi.Services;
using Moq;

namespace InventoryHold.UnitTests.Application;

public class GetInventoryServiceTests
{
    private readonly Mock<IInventoryRepository> _inventory = new();
    private readonly Mock<IInventoryCache>      _cache     = new();
    private readonly InventoryService _service;

    public GetInventoryServiceTests()
    {
        _service = new InventoryService(_inventory.Object, Mock.Of<IHoldRepository>(), _cache.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<InventoryService>.Instance);
    }

    private static IReadOnlyList<InventoryItem> MakeItems() =>
        [new InventoryItem { ProductId = "widget-a", Name = "Widget A", TotalQuantity = 50, AvailableQuantity = 40 }];

    [Fact]
    public async Task GetInventoryAsync_CacheMiss_HitsDbAndSetsCache()
    {
        var items = MakeItems();
        _cache.Setup(c => c.GetInventoryAsync(default)).ReturnsAsync((IReadOnlyList<InventoryItem>?)null);
        _inventory.Setup(r => r.GetAllAsync(default)).ReturnsAsync(items);

        var result = await _service.GetInventoryAsync();

        result.Should().BeEquivalentTo(items);
        _cache.Verify(c => c.SetInventoryAsync(items, default), Times.Once);
    }

    [Fact]
    public async Task GetInventoryAsync_CacheHit_NeverHitsDb()
    {
        var items = MakeItems();
        _cache.Setup(c => c.GetInventoryAsync(default)).ReturnsAsync(items);

        var result = await _service.GetInventoryAsync();

        result.Should().BeEquivalentTo(items);
        _inventory.Verify(r => r.GetAllAsync(default), Times.Never);
    }

    [Fact]
    public async Task GetInventoryAsync_ReturnsCorrectHeldQuantity()
    {
        var items = MakeItems(); // TotalQty=50, AvailableQty=40 → HeldQty=10
        _cache.Setup(c => c.GetInventoryAsync(default)).ReturnsAsync(items);

        var result = await _service.GetInventoryAsync();

        result[0].HeldQuantity.Should().Be(10);
    }

    [Fact]
    public async Task GetInventoryAsync_ConcurrentCacheMiss_FetchesDbOnlyOnce()
    {
        var items = MakeItems();
        // call 1: task1 pre-lock miss; call 2: task1 post-lock miss; call 3: task2 pre-lock hit (populated by task1)
        _cache.SetupSequence(c => c.GetInventoryAsync(default))
              .ReturnsAsync((IReadOnlyList<InventoryItem>?)null)
              .ReturnsAsync((IReadOnlyList<InventoryItem>?)null)
              .ReturnsAsync(items);
        _inventory.Setup(r => r.GetAllAsync(default)).ReturnsAsync(items);

        await Task.WhenAll(_service.GetInventoryAsync(), _service.GetInventoryAsync());

        _inventory.Verify(r => r.GetAllAsync(default), Times.Once);
    }
}
