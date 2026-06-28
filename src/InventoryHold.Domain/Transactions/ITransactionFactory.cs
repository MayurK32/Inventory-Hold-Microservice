namespace InventoryHold.Domain.Transactions;

public interface ITransactionFactory
{
    Task<IMongoTransaction> BeginAsync(CancellationToken ct = default);
}
