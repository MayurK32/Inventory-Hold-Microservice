using FluentAssertions;
using InventoryHold.Domain.Entities;
using InventoryHold.Infrastructure.Persistence;
using InventoryHold.Infrastructure.Persistence.Documents;
using MongoDB.Driver;
using Moq;

namespace InventoryHold.UnitTests.Infrastructure;

public class MongoHoldRepositoryTests
{
    private readonly Mock<IMongoCollection<HoldDocument>> _collection = new();
    private readonly MongoHoldRepository _repo;

    public MongoHoldRepositoryTests()
    {
        _repo = new MongoHoldRepository(_collection.Object);
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

    private static HoldDocument SampleHoldDocument(string id = "test-id-001", HoldStatus status = HoldStatus.Active) =>
        new()
        {
            Id = id, CustomerName = "Test", Status = status,
            Items = [new HoldItemDocument { ProductId = "widget-a", ProductName = "Widget A", Quantity = 1 }],
            CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        };

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsMappedHold()
    {
        var doc = SampleHoldDocument("hold-123");
        _collection.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<HoldDocument>>(),
            It.IsAny<FindOptions<HoldDocument, HoldDocument>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cursor(doc));

        var result = await _repo.GetByIdAsync("hold-123");

        result.Should().NotBeNull();
        result!.Id.Should().Be("hold-123");
        result.Status.Should().Be(HoldStatus.Active);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        _collection.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<HoldDocument>>(),
            It.IsAny<FindOptions<HoldDocument, HoldDocument>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cursor<HoldDocument>());

        var result = await _repo.GetByIdAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsItemsAndCorrectTotal()
    {
        var docs = new[] { SampleHoldDocument("h1"), SampleHoldDocument("h2") };
        _collection.Setup(c => c.EstimatedDocumentCountAsync(
            It.IsAny<EstimatedDocumentCountOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);
        _collection.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<HoldDocument>>(),
            It.IsAny<FindOptions<HoldDocument, HoldDocument>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cursor(docs));

        var (items, total) = await _repo.GetPagedAsync(null, page: 1, pageSize: 2);

        total.Should().Be(10);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task InsertAsync_CallsInsertOneAsync_ReturnsHoldWithSameId()
    {
        _collection.Setup(c => c.InsertOneAsync(
            It.IsAny<HoldDocument>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var hold = Hold.Create(null, [new HoldItem("widget-a", "Widget A", 1)], 15);
        var result = await _repo.InsertAsync(hold);

        result.Id.Should().Be(hold.Id);
        _collection.Verify(c => c.InsertOneAsync(
            It.IsAny<HoldDocument>(),
            It.IsAny<InsertOneOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AtomicTransitionAsync_MatchFound_ReturnsMappedHold()
    {
        var doc = SampleHoldDocument("hold-abc", HoldStatus.Released);
        _collection.Setup(c => c.FindOneAndUpdateAsync(
            It.IsAny<FilterDefinition<HoldDocument>>(),
            It.IsAny<UpdateDefinition<HoldDocument>>(),
            It.IsAny<FindOneAndUpdateOptions<HoldDocument, HoldDocument>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var result = await _repo.AtomicTransitionAsync(
            "hold-abc", HoldStatus.Active, HoldStatus.Released, DateTime.UtcNow);

        result.Should().NotBeNull();
        result!.Status.Should().Be(HoldStatus.Released);
    }

    [Fact]
    public async Task AtomicTransitionAsync_NoMatch_ReturnsNull()
    {
        _collection.Setup(c => c.FindOneAndUpdateAsync(
            It.IsAny<FilterDefinition<HoldDocument>>(),
            It.IsAny<UpdateDefinition<HoldDocument>>(),
            It.IsAny<FindOneAndUpdateOptions<HoldDocument, HoldDocument>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((HoldDocument?)null);

        var result = await _repo.AtomicTransitionAsync(
            "hold-abc", HoldStatus.Active, HoldStatus.Released, DateTime.UtcNow);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetExpiredActiveAsync_ReturnsAllMatchingDocs()
    {
        var doc1 = SampleHoldDocument("h1");
        var doc2 = SampleHoldDocument("h2");
        _collection.Setup(c => c.FindAsync(
            It.IsAny<FilterDefinition<HoldDocument>>(),
            It.IsAny<FindOptions<HoldDocument, HoldDocument>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Cursor(doc1, doc2));

        var result = await _repo.GetExpiredActiveAsync(DateTime.UtcNow);

        result.Should().HaveCount(2);
    }
}
