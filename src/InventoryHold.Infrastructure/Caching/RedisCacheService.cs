using InventoryHold.Domain.Cache;
using InventoryHold.Domain.Entities;
using StackExchange.Redis;
using System.Text.Json;

namespace InventoryHold.Infrastructure.Caching;

internal record HoldCacheDto(
    string Id, string? CustomerName, string Status,
    List<HoldItemCacheDto> Items,
    DateTime CreatedAt, DateTime ExpiresAt,
    DateTime? ReleasedAt, DateTime? ExpiredAt);

internal record HoldItemCacheDto(string ProductId, string ProductName, int Quantity);

public sealed class RedisCacheService(IConnectionMultiplexer multiplexer) : IInventoryCache
{
    private readonly IDatabase _db = multiplexer.GetDatabase();
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private const string InventoryKey = "inventory:all";
    private const string SettingsKey  = "settings:expiration-minutes";
    private static string HoldKey(string id) => $"hold:{id}";

    private static readonly TimeSpan InventoryTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HoldTtl      = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SettingsTtl  = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<InventoryItem>?> GetInventoryAsync(CancellationToken ct = default)
    {
        var val = await _db.StringGetAsync(InventoryKey);
        return val.HasValue
            ? JsonSerializer.Deserialize<List<InventoryItem>>((string)val!, JsonOpts)
            : null;
    }

    public async Task SetInventoryAsync(IReadOnlyList<InventoryItem> items, CancellationToken ct = default) =>
        await _db.StringSetAsync(InventoryKey, JsonSerializer.Serialize(items, JsonOpts), InventoryTtl);

    public async Task InvalidateInventoryAsync(CancellationToken ct = default) =>
        await _db.KeyDeleteAsync(InventoryKey);

    public async Task<Hold?> GetHoldAsync(string holdId, CancellationToken ct = default)
    {
        var val = await _db.StringGetAsync(HoldKey(holdId));
        if (!val.HasValue) return null;
        var dto = JsonSerializer.Deserialize<HoldCacheDto>((string)val!, JsonOpts)!;
        return Hold.Reconstruct(
            dto.Id, dto.CustomerName,
            Enum.Parse<HoldStatus>(dto.Status, ignoreCase: true),
            dto.Items.Select(i => new HoldItem(i.ProductId, i.ProductName, i.Quantity)).ToList(),
            dto.CreatedAt, dto.ExpiresAt, dto.ReleasedAt, dto.ExpiredAt);
    }

    public async Task SetHoldAsync(Hold hold, CancellationToken ct = default)
    {
        var dto = new HoldCacheDto(
            hold.Id, hold.CustomerName, hold.Status.ToString(),
            hold.Items.Select(i => new HoldItemCacheDto(i.ProductId, i.ProductName, i.Quantity)).ToList(),
            hold.CreatedAt, hold.ExpiresAt, hold.ReleasedAt, hold.ExpiredAt);
        await _db.StringSetAsync(HoldKey(hold.Id), JsonSerializer.Serialize(dto, JsonOpts), HoldTtl);
    }

    public async Task InvalidateHoldAsync(string holdId, CancellationToken ct = default) =>
        await _db.KeyDeleteAsync(HoldKey(holdId));

    public async Task<int?> GetExpirationMinutesAsync(CancellationToken ct = default)
    {
        var val = await _db.StringGetAsync(SettingsKey);
        return val.HasValue && int.TryParse((string)val, out var m) ? m : null;
    }

    public async Task SetExpirationMinutesAsync(int minutes, CancellationToken ct = default) =>
        await _db.StringSetAsync(SettingsKey, minutes.ToString(), SettingsTtl);

    public async Task FlushAllAsync(CancellationToken ct = default)
    {
        await _db.KeyDeleteAsync(InventoryKey);
        await _db.KeyDeleteAsync(SettingsKey);
        // Individual hold keys expire via their 60s TTL
    }
}
