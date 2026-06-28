using InventoryHold.Domain.Transactions;
using MongoDB.Driver;

namespace InventoryHold.Infrastructure.Transactions;

public sealed class MongoTransactionFactory : ITransactionFactory
{
    private readonly IMongoClient _client;

    public MongoTransactionFactory(IMongoClient client) => _client = client;

    public async Task<IMongoTransaction> BeginAsync(CancellationToken ct = default)
    {
        var session = await _client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();
        return new MongoTransaction(session);
    }
}
