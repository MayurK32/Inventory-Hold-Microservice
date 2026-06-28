using InventoryHold.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace InventoryHold.Infrastructure.Persistence;

public sealed class CollectionIndexInitializer(
    IMongoCollection<HoldDocument> holds,
    IMongoCollection<InventoryDocument> inventory)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await holds.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<HoldDocument>(
                Builders<HoldDocument>.IndexKeys
                    .Ascending(h => h.Status)
                    .Ascending(h => h.ExpiresAt)),
            new CreateIndexModel<HoldDocument>(
                Builders<HoldDocument>.IndexKeys
                    .Ascending(h => h.Status)
                    .Descending(h => h.CreatedAt))
        }, ct);

        await inventory.Indexes.CreateOneAsync(
            new CreateIndexModel<InventoryDocument>(
                Builders<InventoryDocument>.IndexKeys.Ascending(i => i.ProductId),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: ct);
    }
}
