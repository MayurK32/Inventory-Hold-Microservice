using FluentAssertions;
using InventoryHold.Contracts.Settings;
using InventoryHold.Domain.Cache;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Exceptions;
using InventoryHold.Domain.Messaging;
using InventoryHold.Domain.Repositories;
using InventoryHold.Domain.Transactions;
using InventoryHold.WebApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace InventoryHold.UnitTests.Application;

public class GetHoldServiceTests
{
    private readonly Mock<IHoldRepository>      _holds     = new();
    private readonly Mock<IInventoryCache>      _cache     = new();
    private readonly HoldService _service;

    public GetHoldServiceTests()
    {
        _service = new HoldService(
            _holds.Object, Mock.Of<IInventoryRepository>(),
            Mock.Of<ISettingsRepository>(), Mock.Of<ITransactionFactory>(),
            Options.Create(new HoldSettings()), _cache.Object,
            Mock.Of<IHoldEventPublisher>(), NullLogger<HoldService>.Instance);
    }

    private static Hold MakeActiveHold(string id = "h1") =>
        Hold.Reconstruct(id, "Alice", HoldStatus.Active,
            [new HoldItem("widget-a", "Widget A", 3)],
            DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(10), null, null);

    [Fact]
    public async Task GetHoldAsync_CacheMiss_HitsDbAndSetsCache()
    {
        var hold = MakeActiveHold();
        _cache.Setup(c => c.GetHoldAsync("h1", default)).ReturnsAsync((Hold?)null);
        _holds.Setup(r => r.GetByIdAsync("h1", default)).ReturnsAsync(hold);

        var result = await _service.GetHoldAsync("h1");

        result.Should().BeEquivalentTo(hold);
        _cache.Verify(c => c.SetHoldAsync(hold, default), Times.Once);
    }

    [Fact]
    public async Task GetHoldAsync_CacheHit_NeverHitsDb()
    {
        var hold = MakeActiveHold();
        _cache.Setup(c => c.GetHoldAsync("h1", default)).ReturnsAsync(hold);

        var result = await _service.GetHoldAsync("h1");

        result.Should().BeEquivalentTo(hold);
        _holds.Verify(r => r.GetByIdAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task GetHoldAsync_NotFound_ThrowsHoldNotFoundException()
    {
        _cache.Setup(c => c.GetHoldAsync("x", default)).ReturnsAsync((Hold?)null);
        _holds.Setup(r => r.GetByIdAsync("x", default)).ReturnsAsync((Hold?)null);

        await FluentActions.Invoking(() => _service.GetHoldAsync("x"))
            .Should().ThrowAsync<HoldNotFoundException>();
    }
}
