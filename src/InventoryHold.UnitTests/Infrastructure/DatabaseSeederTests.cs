using FluentAssertions;
using InventoryHold.Infrastructure.Persistence;
using InventoryHold.Infrastructure.Persistence.Documents;
using MongoDB.Driver;
using Moq;

namespace InventoryHold.UnitTests.Infrastructure;

public class DatabaseSeederTests
{
    private readonly Mock<IMongoCollection<InventoryDocument>> _inventory = new();

    [Fact]
    public async Task SeedAsync_EmptyCollection_InsertsFiveProducts()
    {
        _inventory.Setup(c => c.CountDocumentsAsync(
            It.IsAny<FilterDefinition<InventoryDocument>>(),
            It.IsAny<CountOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await new DatabaseSeeder(_inventory.Object).SeedAsync();

        _inventory.Verify(c => c.InsertManyAsync(
            It.Is<IEnumerable<InventoryDocument>>(d => d.Count() == 5),
            It.IsAny<InsertManyOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SeedAsync_NonEmptyCollection_SkipsInsert()
    {
        _inventory.Setup(c => c.CountDocumentsAsync(
            It.IsAny<FilterDefinition<InventoryDocument>>(),
            It.IsAny<CountOptions>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        await new DatabaseSeeder(_inventory.Object).SeedAsync();

        _inventory.Verify(c => c.InsertManyAsync(
            It.IsAny<IEnumerable<InventoryDocument>>(),
            It.IsAny<InsertManyOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
