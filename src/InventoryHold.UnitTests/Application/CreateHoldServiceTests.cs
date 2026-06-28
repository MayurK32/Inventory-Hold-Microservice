using System.Net;
using FluentAssertions;
using InventoryHold.Contracts.Requests;
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
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using Moq;

namespace InventoryHold.UnitTests.Application;

public class CreateHoldServiceTests
{
    private readonly Mock<IHoldRepository>      _holds     = new();
    private readonly Mock<IInventoryRepository> _inventory = new();
    private readonly Mock<ISettingsRepository>  _settings  = new();
    private readonly Mock<ITransactionFactory>  _txFactory = new();
    private readonly Mock<IMongoTransaction>    _tx        = new();
    private readonly Mock<IInventoryCache>      _cache     = new();
    private readonly Mock<IHoldEventPublisher>  _publisher = new();
    private readonly HoldService _service;

    private static readonly InventoryItem WidgetA = new()
    {
        ProductId = "widget-a", Name = "Widget A",
        TotalQuantity = 50, AvailableQuantity = 10
    };

    public CreateHoldServiceTests()
    {
        _settings.Setup(s => s.GetExpirationMinutesAsync(It.IsAny<int>(), default))
                 .ReturnsAsync(15);
        _txFactory.Setup(f => f.BeginAsync(default)).ReturnsAsync(_tx.Object);

        _service = new HoldService(
            _holds.Object, _inventory.Object, _settings.Object, _txFactory.Object,
            Options.Create(new HoldSettings { ExpirationMinutes = 15 }), _cache.Object,
            _publisher.Object, NullLogger<HoldService>.Instance);
    }

    // Happy path

    [Fact]
    public async Task CreateHoldAsync_AllInStock_ReturnsHoldWithDenormalizedProductName()
    {
        _inventory.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<string>>(), default)).ReturnsAsync(new List<InventoryItem> { WidgetA });
        _holds.Setup(r => r.InsertAsync(It.IsAny<Hold>(), _tx.Object, default))
              .ReturnsAsync((Hold h, IMongoTransaction? _, CancellationToken _) => h);

        var result = await _service.CreateHoldAsync(new("John", [new("widget-a", 3)]), default);

        result.Items.Should().HaveCount(1);
        result.Items[0].ProductName.Should().Be("Widget A");
        result.Items[0].Quantity.Should().Be(3);
        result.Status.Should().Be(HoldStatus.Active);
        _tx.Verify(t => t.CommitAsync(default), Times.Once);
        _inventory.Verify(r => r.DecrementBatchAsync(
            It.IsAny<IReadOnlyList<HoldItem>>(), _tx.Object, default), Times.Once);
    }

    // Early validation — no DB calls

    [Fact]
    public async Task CreateHoldAsync_EmptyItems_ThrowsDomainException()
    {
        await FluentActions.Invoking(() => _service.CreateHoldAsync(new(null, []), default))
            .Should().ThrowAsync<DomainException>();
        _txFactory.Verify(f => f.BeginAsync(default), Times.Never);
    }

    [Fact]
    public async Task CreateHoldAsync_ZeroQuantity_ThrowsDomainException()
    {
        await FluentActions.Invoking(() =>
            _service.CreateHoldAsync(new(null, [new("widget-a", 0)]), default))
            .Should().ThrowAsync<DomainException>();
        _txFactory.Verify(f => f.BeginAsync(default), Times.Never);
    }

    [Fact]
    public async Task CreateHoldAsync_TooManyItems_ThrowsDomainException()
    {
        var items = Enumerable.Range(0, 51).Select(i => new CreateHoldItemRequest($"p{i}", 1)).ToList();
        await FluentActions.Invoking(() => _service.CreateHoldAsync(new(null, items), default))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*50*");
        _txFactory.Verify(f => f.BeginAsync(default), Times.Never);
    }

    // Stock errors

    [Fact]
    public async Task CreateHoldAsync_ProductNotFound_ThrowsProductNotFoundException()
    {
        _inventory.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<string>>(), default))
                  .ReturnsAsync(new List<InventoryItem>());
        await FluentActions.Invoking(() =>
            _service.CreateHoldAsync(new(null, [new("unknown", 1)]), default))
            .Should().ThrowAsync<ProductNotFoundException>();
    }

    [Fact]
    public async Task CreateHoldAsync_InsufficientStock_ThrowsWithAllFailures()
    {
        _inventory.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<string>>(), default))
                  .ReturnsAsync(new List<InventoryItem> { new() { ProductId = "widget-a", Name = "Widget A", TotalQuantity = 50, AvailableQuantity = 1 } });

        var ex = await FluentActions.Invoking(() =>
            _service.CreateHoldAsync(new(null, [new("widget-a", 5)]), default))
            .Should().ThrowAsync<InsufficientStockException>();

        ex.Which.Failures.Should().HaveCount(1);
        ex.Which.Failures[0].ProductId.Should().Be("widget-a");
        ex.Which.Failures[0].Requested.Should().Be(5);
        ex.Which.Failures[0].Available.Should().Be(1);
    }

    [Fact]
    public async Task CreateHoldAsync_InsufficientStock_NeverDecrementsInventory()
    {
        _inventory.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<string>>(), default))
                  .ReturnsAsync(new List<InventoryItem> { new() { ProductId = "widget-a", Name = "Widget A", TotalQuantity = 50, AvailableQuantity = 1 } });

        await Assert.ThrowsAsync<InsufficientStockException>(() =>
            _service.CreateHoldAsync(new(null, [new("widget-a", 5)]), default));

        _inventory.Verify(r => r.DecrementBatchAsync(
            It.IsAny<IReadOnlyList<HoldItem>>(), It.IsAny<IMongoTransaction>(), default),
            Times.Never);
    }

    // Event publishing

    [Fact]
    public async Task CreateHoldAsync_Success_PublishesHoldCreatedEvent()
    {
        _inventory.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<string>>(), default)).ReturnsAsync(new List<InventoryItem> { WidgetA });
        _holds.Setup(r => r.InsertAsync(It.IsAny<Hold>(), _tx.Object, default))
              .ReturnsAsync((Hold h, IMongoTransaction? _, CancellationToken _) => h);

        await _service.CreateHoldAsync(new("John", [new("widget-a", 3)]), default);

        _publisher.Verify(p => p.PublishHoldCreatedAsync(It.IsAny<Hold>(), default), Times.Once);
    }

    // Cache behaviour

    [Fact]
    public async Task CreateHoldAsync_Success_InvalidatesInventoryCache()
    {
        _inventory.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<string>>(), default)).ReturnsAsync(new List<InventoryItem> { WidgetA });
        _holds.Setup(r => r.InsertAsync(It.IsAny<Hold>(), _tx.Object, default))
              .ReturnsAsync((Hold h, IMongoTransaction? _, CancellationToken _) => h);

        await _service.CreateHoldAsync(new("Alice", [new("widget-a", 1)]), default);

        _cache.Verify(c => c.InvalidateInventoryAsync(default), Times.Once);
    }

    [Fact]
    public async Task CreateHoldAsync_ExpirationMinutesCached_SkipsSettingsRepository()
    {
        _cache.Setup(c => c.GetExpirationMinutesAsync(default)).ReturnsAsync(15);
        _inventory.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<string>>(), default)).ReturnsAsync(new List<InventoryItem> { WidgetA });
        _holds.Setup(r => r.InsertAsync(It.IsAny<Hold>(), _tx.Object, default))
              .ReturnsAsync((Hold h, IMongoTransaction? _, CancellationToken _) => h);

        await _service.CreateHoldAsync(new("Alice", [new("widget-a", 1)]), default);

        _settings.Verify(r => r.GetExpirationMinutesAsync(It.IsAny<int>(), default), Times.Never);
    }

    // Write conflict retry

    [Fact]
    public async Task CreateHoldAsync_WriteConflict_RetriesThreeTimesAndThrows()
    {
        _inventory.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<string>>(), default)).ReturnsAsync(new List<InventoryItem> { WidgetA });
        _inventory.Setup(r => r.DecrementBatchAsync(
            It.IsAny<IReadOnlyList<HoldItem>>(), It.IsAny<IMongoTransaction>(), default))
            .ThrowsAsync(MakeWriteConflict());

        await FluentActions.Invoking(() =>
            _service.CreateHoldAsync(new(null, [new("widget-a", 1)]), default))
            .Should().ThrowAsync<StockUnavailableException>();

        _inventory.Verify(r => r.DecrementBatchAsync(
            It.IsAny<IReadOnlyList<HoldItem>>(), It.IsAny<IMongoTransaction>(), default),
            Times.Exactly(3));
    }

    [Fact]
    public async Task CreateHoldAsync_NonConflictException_DoesNotRetry()
    {
        _inventory.Setup(r => r.GetByProductIdsAsync(It.IsAny<IEnumerable<string>>(), default)).ReturnsAsync(new List<InventoryItem> { WidgetA });
        _inventory.Setup(r => r.DecrementBatchAsync(
            It.IsAny<IReadOnlyList<HoldItem>>(), It.IsAny<IMongoTransaction>(), default))
            .ThrowsAsync(new InvalidOperationException("not a conflict"));

        await FluentActions.Invoking(() =>
            _service.CreateHoldAsync(new(null, [new("widget-a", 1)]), default))
            .Should().ThrowAsync<InvalidOperationException>();

        _inventory.Verify(r => r.DecrementBatchAsync(
            It.IsAny<IReadOnlyList<HoldItem>>(), It.IsAny<IMongoTransaction>(), default),
            Times.Once);
    }

    private static MongoCommandException MakeWriteConflict()
    {
        var cid = new ConnectionId(
            new ServerId(new ClusterId(1), new DnsEndPoint("localhost", 27017)), 1);
        var result = new BsonDocument { { "code", new BsonInt32(112) }, { "errmsg", new BsonString("WriteConflict") } };
        return new MongoCommandException(cid, "WriteConflict", result);
    }
}
