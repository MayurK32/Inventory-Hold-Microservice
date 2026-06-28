namespace InventoryHold.Domain.Transactions;

public interface IMongoTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct = default);
    Task AbortAsync(CancellationToken ct = default);
}
