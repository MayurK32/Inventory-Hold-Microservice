using InventoryHold.Domain.Repositories;
using InventoryHold.Infrastructure.Persistence.Documents;
using MongoDB.Driver;

namespace InventoryHold.Infrastructure.Persistence;

public sealed class MongoSettingsRepository(IMongoCollection<AppSettingDocument> settings)
    : ISettingsRepository
{
    public async Task<int> GetExpirationMinutesAsync(int defaultMinutes, CancellationToken ct = default)
    {
        var doc = await settings
            .Find(s => s.Key == "HoldExpirationMinutes")
            .FirstOrDefaultAsync(ct);

        return doc?.Value.AsNullableInt32 ?? defaultMinutes;
    }
}
