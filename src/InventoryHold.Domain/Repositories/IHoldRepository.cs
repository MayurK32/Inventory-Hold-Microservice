using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Transactions;

namespace InventoryHold.Domain.Repositories;

public interface IHoldRepository
{
    Task<Hold?> GetByIdAsync(string holdId, CancellationToken ct = default);
    Task<(IReadOnlyList<Hold> Items, long Total)> GetPagedAsync(string? status, int page, int pageSize, CancellationToken ct = default);
    Task<Hold> InsertAsync(Hold hold, IMongoTransaction? transaction = null, CancellationToken ct = default);
    Task<Hold?> AtomicTransitionAsync(string holdId, HoldStatus expectedStatus, HoldStatus newStatus, DateTime transitionTime, CancellationToken ct = default);
    Task<IReadOnlyList<Hold>> GetExpiredActiveAsync(DateTime asOf, CancellationToken ct = default);
}
