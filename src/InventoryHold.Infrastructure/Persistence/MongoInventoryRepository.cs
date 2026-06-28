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

    public async Task<IReadOnlyList<InventoryItem>> GetByProductIdsAsync(
        IEnumerable<string> productIds, CancellationToken ct = default)
    {
        var filter = Builders<InventoryDocument>.Filter.In(i => i.ProductId, productIds);
        var cursor = await collection.FindAsync(filter, cancellationToken: ct);
        var docs = await cursor.ToListAsync(ct);
        return docs.Select(d => d.ToDomain()).ToList();
    }

    public async Task DecrementBatchAsync(
        IReadOnlyList<HoldItem> items, IMongoTransaction transaction, CancellationToken ct = default)
    {
        var writes = items.Select(item => new UpdateOneModel<InventoryDocument>(
            Builders<InventoryDocument>.Filter.Eq(i => i.ProductId, item.ProductId),
            Builders<InventoryDocument>.Update.Inc(i => i.AvailableQuantity, -item.Quantity))
        ).ToList<WriteModel<InventoryDocument>>();

        var session = GetSession(transaction);
        var opts = new BulkWriteOptions { IsOrdered = false };
        if (session is not null)
            await collection.BulkWriteAsync(session, writes, opts, ct);
        else
            await collection.BulkWriteAsync(writes, opts, ct);
    }

    public async Task IncrementAsync(IReadOnlyList<HoldItem> items, CancellationToken ct = default)
    {
        var writes = items.Select(item => new UpdateOneModel<InventoryDocument>(
            Builders<InventoryDocument>.Filter.Eq(i => i.ProductId, item.ProductId),
            Builders<InventoryDocument>.Update.Inc(i => i.AvailableQuantity, item.Quantity))
        ).ToList<WriteModel<InventoryDocument>>();

        await collection.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false }, ct);
    }

    public async Task ResetAllAsync(CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        var writes = all.Select(item => new UpdateOneModel<InventoryDocument>(
            Builders<InventoryDocument>.Filter.Eq(i => i.ProductId, item.ProductId),
            Builders<InventoryDocument>.Update.Set(i => i.AvailableQuantity, item.TotalQuantity))
        ).ToList<WriteModel<InventoryDocument>>();

        if (writes.Count > 0)
            await collection.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = false }, ct);
    }
}
