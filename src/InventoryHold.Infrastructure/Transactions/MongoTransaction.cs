using InventoryHold.Domain.Transactions;
using MongoDB.Driver;

namespace InventoryHold.Infrastructure.Transactions;

public sealed class MongoTransaction : IMongoTransaction
{
    private readonly IClientSessionHandle _session;
    internal IClientSessionHandle Session => _session;

    public MongoTransaction(IClientSessionHandle session) => _session = session;

    public Task CommitAsync(CancellationToken ct = default) => _session.CommitTransactionAsync(ct);
    public Task AbortAsync(CancellationToken ct = default)  => _session.AbortTransactionAsync(ct);

    public ValueTask DisposeAsync()
    {
        _session.Dispose();
        return ValueTask.CompletedTask;
    }
}
