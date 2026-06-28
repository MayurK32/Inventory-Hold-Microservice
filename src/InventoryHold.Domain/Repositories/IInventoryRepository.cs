using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Transactions;

namespace InventoryHold.Domain.Repositories;

public interface IInventoryRepository
{
    Task<IReadOnlyList<InventoryItem>> GetAllAsync(CancellationToken ct = default);
    Task<InventoryItem?> GetByProductIdAsync(string productId, CancellationToken ct = default);
    Task DecrementBatchAsync(IReadOnlyList<HoldItem> items, IMongoTransaction transaction, CancellationToken ct = default);
    Task IncrementAsync(IReadOnlyList<HoldItem> items, CancellationToken ct = default);
    Task ResetAllAsync(CancellationToken ct = default);
}
