using FluentAssertions;
using InventoryHold.Domain.Entities;
using InventoryHold.Infrastructure.Persistence;
using InventoryHold.Infrastructure.Persistence.Documents;
using InventoryHold.Infrastructure.Transactions;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;

namespace InventoryHold.UnitTests.Infrastructure;

public class MongoInventoryRepositoryTests
{
    private readonly Mock<IMongoCollection<InventoryDocument>> _collection = new();
    private readonly MongoInventoryRepository _repo;

    public MongoInventoryRepositoryTests()
    {
        _repo = new MongoInventoryRepository(_collection.Object);
    }

    private static IAsyncCursor<T> Cursor<T>(params T[] items)
    {
        var mock = new Mock<IAsyncCursor<T>>();
        if (items.Length > 0)
        {
            mock.SetupSequence(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true).ReturnsAsync(false);
            mock.Setup(c => c.Current).Returns(items);
        }
        else
        {
            mock.Setup(c => c.MoveNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        }
        return mock.Object;
    }

    [Fact]
    public async Task GetAllAsync_ReturnsMappedItems_WithComputedHeldQuantity()
    {
        var doc = new InventoryDocument
        {
            Id = ObjectId.GenerateNewId(), ProductId = "widget-a", Name = "Widget A",
            TotalQuantity = 50, AvailableQuantity = 48, CreatedAt = DateTime.UtcNow
        };
        _collection.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<InventoryDocument>>(),
            It.IsAny<FindOptions<InventoryDocument, InventoryDocument>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cursor(doc));

        var result = await _repo.GetAllAsync();

        result.Should().HaveCount(1);
        result[0].ProductId.Should().Be("widget-a");
        result[0].HeldQuantity.Should().Be(2);
    }

    [Fact]
    public async Task GetByProductIdAsync_Found_ReturnsMappedItem()
    {
        var doc = new InventoryDocument
        {
            Id = ObjectId.GenerateNewId(), ProductId = "widget-a", Name = "Widget A",
            TotalQuantity = 50, AvailableQuantity = 50, CreatedAt = DateTime.UtcNow
        };
        _collection.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<InventoryDocument>>(),
            It.IsAny<FindOptions<InventoryDocument, InventoryDocument>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cursor(doc));

        var result = await _repo.GetByProductIdAsync("widget-a");

        result.Should().NotBeNull();
        result!.ProductId.Should().Be("widget-a");
    }

    [Fact]
    public async Task GetByProductIdAsync_NotFound_ReturnsNull()
    {
        _collection.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<InventoryDocument>>(),
            It.IsAny<FindOptions<InventoryDocument, InventoryDocument>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cursor<InventoryDocument>());

        var result = await _repo.GetByProductIdAsync("unknown");

        result.Should().BeNull();
    }

    [Fact]
    public async Task DecrementBatchAsync_CallsBulkWriteWithSession()
    {
        _collection.Setup(c => c.BulkWriteAsync(
            It.IsAny<IClientSessionHandle>(),
            It.IsAny<IEnumerable<WriteModel<InventoryDocument>>>(),
            It.IsAny<BulkWriteOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<BulkWriteResult<InventoryDocument>>(null!));

        var session = new Mock<IClientSessionHandle>();
        var transaction = new MongoTransaction(session.Object);
        var items = new[] { new HoldItem("widget-a", "Widget A", 3) };

        await _repo.DecrementBatchAsync(items, transaction);

        _collection.Verify(c => c.BulkWriteAsync(
            It.IsAny<IClientSessionHandle>(),
            It.IsAny<IEnumerable<WriteModel<InventoryDocument>>>(),
            It.IsAny<BulkWriteOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IncrementAsync_CallsBulkWrite_NoSession()
    {
        _collection.Setup(c => c.BulkWriteAsync(
            It.IsAny<IEnumerable<WriteModel<InventoryDocument>>>(),
            It.IsAny<BulkWriteOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<BulkWriteResult<InventoryDocument>>(null!));

        var items = new[] { new HoldItem("widget-a", "Widget A", 3) };

        await _repo.IncrementAsync(items);

        _collection.Verify(c => c.BulkWriteAsync(
            It.IsAny<IEnumerable<WriteModel<InventoryDocument>>>(),
            It.IsAny<BulkWriteOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
