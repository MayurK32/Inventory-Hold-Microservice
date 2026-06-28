namespace InventoryHold.Domain.Repositories;

public interface ISettingsRepository
{
    Task<int> GetExpirationMinutesAsync(int defaultMinutes, CancellationToken ct = default);
}
