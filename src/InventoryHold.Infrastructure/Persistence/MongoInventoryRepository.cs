using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Repositories;
using InventoryHold.Domain.Transactions;
using InventoryHold.Infrastructure.Persistence.Documents;
using InventoryHold.Infrastructure.Transactions;
using MongoDB.Driver;

namespace InventoryHold.Infrastructure.Persistence;

public sealed class MongoInventoryRepository(IMongoCollection<InventoryDocument> collection)
    : IInventoryRepository
{
    private static IClientSessionHandle? GetSession(IMongoTransaction? t) =>
        (t as MongoTransaction)?.Session;

    public async Task<IReadOnlyList<InventoryItem>> GetAllAsync(CancellationToken ct = default)
    {
        var cursor = await collection.FindAsync(
            FilterDefinition<InventoryDocument>.Empty, cancellationToken: ct);
        var docs = await cursor.ToListAsync(ct);
        return docs.Select(d => d.ToDomain()).ToList();
    }

    public async Task<InventoryItem?> GetByProductIdAsync(string productId, CancellationToken ct = default)
    {
        var cursor = await collection.FindAsync(
            Builders<InventoryDocument>.Filter.Eq(i => i.ProductId, productId),
            cancellationToken: ct);
        var doc = await cursor.FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public async Task DecrementBatchAsync(
        IReadOnlyList<HoldItem> items, IMongoTransaction transaction, CancellationToken ct = default)
    {
        var session = GetSession(transaction);
        foreach (var item in items)
        {
            var filter = Builders<InventoryDocument>.Filter.Eq(i => i.ProductId, item.ProductId);
            var update = Builders<InventoryDocument>.Update.Inc(i => i.AvailableQuantity, -item.Quantity);
            if (session is not null)
                await collection.UpdateOneAsync(session, filter, update, cancellationToken: ct);
            else
                await collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        }
    }

    public async Task IncrementAsync(IReadOnlyList<HoldItem> items, CancellationToken ct = default)
    {
        foreach (var item in items)
        {
            await collection.UpdateOneAsync(
                Builders<InventoryDocument>.Filter.Eq(i => i.ProductId, item.ProductId),
                Builders<InventoryDocument>.Update.Inc(i => i.AvailableQuantity, item.Quantity),
                cancellationToken: ct);
        }
    }

    public async Task ResetAllAsync(CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        foreach (var item in all)
        {
            await collection.UpdateOneAsync(
                Builders<InventoryDocument>.Filter.Eq(i => i.ProductId, item.ProductId),
                Builders<InventoryDocument>.Update.Set(i => i.AvailableQuantity, item.TotalQuantity),
                cancellationToken: ct);
        }
    }
}
