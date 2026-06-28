using InventoryHold.Infrastructure.Persistence.Documents;
using MongoDB.Bson;
using MongoDB.Driver;

namespace InventoryHold.Infrastructure.Persistence;

public sealed class DatabaseSeeder(IMongoCollection<InventoryDocument> inventory)
{
    public static readonly InventoryDocument[] SeedItems =
    [
        new() { ProductId = "widget-a", Name = "Widget A",       TotalQuantity = 50,  AvailableQuantity = 50,  CreatedAt = DateTime.UtcNow },
        new() { ProductId = "widget-b", Name = "Widget B",       TotalQuantity = 30,  AvailableQuantity = 30,  CreatedAt = DateTime.UtcNow },
        new() { ProductId = "gadget-x", Name = "Gadget X",       TotalQuantity = 20,  AvailableQuantity = 20,  CreatedAt = DateTime.UtcNow },
        new() { ProductId = "device-z", Name = "Device Z",       TotalQuantity = 10,  AvailableQuantity = 10,  CreatedAt = DateTime.UtcNow },
        new() { ProductId = "part-001", Name = "Spare Part 001", TotalQuantity = 100, AvailableQuantity = 100, CreatedAt = DateTime.UtcNow }
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var count = await inventory.CountDocumentsAsync(
            FilterDefinition<InventoryDocument>.Empty, cancellationToken: ct);

        if (count == 0)
            await inventory.InsertManyAsync(SeedItems, cancellationToken: ct);
    }
}
