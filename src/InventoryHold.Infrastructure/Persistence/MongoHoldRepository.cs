using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Repositories;
using InventoryHold.Domain.Transactions;
using InventoryHold.Infrastructure.Persistence.Documents;
using InventoryHold.Infrastructure.Transactions;
using MongoDB.Driver;

namespace InventoryHold.Infrastructure.Persistence;

public sealed class MongoHoldRepository(IMongoCollection<HoldDocument> collection)
    : IHoldRepository
{
    private static IClientSessionHandle? GetSession(IMongoTransaction? t) =>
        (t as MongoTransaction)?.Session;

    public async Task<Hold?> GetByIdAsync(string holdId, CancellationToken ct = default)
    {
        var cursor = await collection.FindAsync(
            Builders<HoldDocument>.Filter.Eq(h => h.Id, holdId), cancellationToken: ct);
        var doc = await cursor.FirstOrDefaultAsync(ct);
        return doc?.ToDomain();
    }

    public async Task<(IReadOnlyList<Hold> Items, long Total)> GetPagedAsync(
        string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var filter = status is null
            ? FilterDefinition<HoldDocument>.Empty
            : Builders<HoldDocument>.Filter.Eq(h => h.Status,
                Enum.Parse<HoldStatus>(status, ignoreCase: true));

        var total = await collection.CountDocumentsAsync(filter, cancellationToken: ct);

        var cursor = await collection.FindAsync(filter, new FindOptions<HoldDocument>
        {
            Sort = Builders<HoldDocument>.Sort.Descending(h => h.CreatedAt),
            Skip = (page - 1) * pageSize,
            Limit = pageSize
        }, ct);
        var docs = await cursor.ToListAsync(ct);

        return (docs.Select(d => d.ToDomain()).ToList(), total);
    }

    public async Task<Hold> InsertAsync(
        Hold hold, IMongoTransaction? transaction = null, CancellationToken ct = default)
    {
        var doc = HoldDocument.FromDomain(hold);
        var session = GetSession(transaction);
        if (session is not null)
            await collection.InsertOneAsync(session, doc, cancellationToken: ct);
        else
            await collection.InsertOneAsync(doc, cancellationToken: ct);
        return doc.ToDomain();
    }

    public async Task<Hold?> AtomicTransitionAsync(
        string holdId, HoldStatus expectedStatus, HoldStatus newStatus,
        DateTime transitionTime, CancellationToken ct = default)
    {
        var filter = Builders<HoldDocument>.Filter.And(
            Builders<HoldDocument>.Filter.Eq(h => h.Id, holdId),
            Builders<HoldDocument>.Filter.Eq(h => h.Status, expectedStatus));

        var update = newStatus == HoldStatus.Released
            ? Builders<HoldDocument>.Update
                .Set(h => h.Status, HoldStatus.Released)
                .Set(h => h.ReleasedAt, transitionTime)
            : Builders<HoldDocument>.Update
                .Set(h => h.Status, HoldStatus.Expired)
                .Set(h => h.ExpiredAt, transitionTime);

        var result = await collection.FindOneAndUpdateAsync(filter, update,
            new FindOneAndUpdateOptions<HoldDocument> { ReturnDocument = ReturnDocument.After },
            ct);

        return result?.ToDomain();
    }

    public async Task<IReadOnlyList<Hold>> GetExpiredActiveAsync(DateTime asOf, CancellationToken ct = default)
    {
        var filter = Builders<HoldDocument>.Filter.And(
            Builders<HoldDocument>.Filter.Eq(h => h.Status, HoldStatus.Active),
            Builders<HoldDocument>.Filter.Lte(h => h.ExpiresAt, asOf));

        var cursor = await collection.FindAsync(filter, cancellationToken: ct);
        var docs = await cursor.ToListAsync(ct);
        return docs.Select(d => d.ToDomain()).ToList();
    }

    public async Task DeleteAllAsync(CancellationToken ct = default) =>
        await collection.DeleteManyAsync(Builders<HoldDocument>.Filter.Empty, ct);
}
