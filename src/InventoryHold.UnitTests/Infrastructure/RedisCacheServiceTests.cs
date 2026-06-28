using FluentAssertions;
using InventoryHold.Domain.Entities;
using InventoryHold.Infrastructure.Caching;
using Moq;
using StackExchange.Redis;
using System.Text.Json;

namespace InventoryHold.UnitTests.Infrastructure;

public class RedisCacheServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _multiplexer = new();
    private readonly Mock<IDatabase> _db = new();
    private readonly RedisCacheService _cache;

    private static readonly JsonSerializerOptions CamelCase =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public RedisCacheServiceTests()
    {
        _multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                    .Returns(_db.Object);
        _cache = new RedisCacheService(_multiplexer.Object);
    }

    // ── Inventory ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetInventoryAsync_CacheHit_ReturnsDeserializedList()
    {
        var items = new List<InventoryItem>
        {
            new() { ProductId = "widget-a", Name = "Widget A", TotalQuantity = 50, AvailableQuantity = 47 }
        };
        var json = JsonSerializer.Serialize(items, CamelCase);

        _db.Setup(d => d.StringGetAsync("inventory:all", CommandFlags.None))
           .ReturnsAsync((RedisValue)json);

        var result = await _cache.GetInventoryAsync();

        result.Should().NotBeNull();
        result!.Should().HaveCount(1);
        result[0].ProductId.Should().Be("widget-a");
    }

    [Fact]
    public async Task GetInventoryAsync_CacheMiss_ReturnsNull()
    {
        _db.Setup(d => d.StringGetAsync("inventory:all", CommandFlags.None))
           .ReturnsAsync(RedisValue.Null);

        var result = await _cache.GetInventoryAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetInventoryAsync_SerializesAndSetsWithThirtySecondTtl()
    {
        var items = new List<InventoryItem>
        {
            new() { ProductId = "widget-a", Name = "Widget A", TotalQuantity = 50, AvailableQuantity = 50 }
        };

        await _cache.SetInventoryAsync(items);

        var inv = _db.Invocations.First(i => i.Method.Name == "StringSetAsync");
        ((string)(RedisKey)inv.Arguments[0]).Should().Be("inventory:all");
        ((Expiration)inv.Arguments[2]).Should().Be((Expiration)TimeSpan.FromSeconds(30));
        var doc = JsonSerializer.Deserialize<JsonElement>((string)(RedisValue)inv.Arguments[1], CamelCase);
        doc[0].GetProperty("productId").GetString().Should().Be("widget-a");
    }

    [Fact]
    public async Task InvalidateInventoryAsync_DeletesInventoryKey()
    {
        _db.Setup(d => d.KeyDeleteAsync("inventory:all", CommandFlags.None)).ReturnsAsync(true);

        await _cache.InvalidateInventoryAsync();

        _db.Verify(d => d.KeyDeleteAsync("inventory:all", CommandFlags.None), Times.Once);
    }

    // ── Hold ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetHoldAsync_CacheHit_ReturnsReconstructedHold()
    {
        var json = """
            {
              "id": "hold-123",
              "customerName": "Alice",
              "status": "Active",
              "items": [{"productId": "widget-a", "productName": "Widget A", "quantity": 3}],
              "createdAt": "2024-01-01T00:00:00Z",
              "expiresAt": "2024-01-01T00:15:00Z",
              "releasedAt": null,
              "expiredAt": null
            }
            """;

        _db.Setup(d => d.StringGetAsync("hold:hold-123", CommandFlags.None))
           .ReturnsAsync((RedisValue)json);

        var result = await _cache.GetHoldAsync("hold-123");

        result.Should().NotBeNull();
        result!.Id.Should().Be("hold-123");
        result.CustomerName.Should().Be("Alice");
        result.Status.Should().Be(HoldStatus.Active);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetHoldAsync_CacheMiss_ReturnsNull()
    {
        _db.Setup(d => d.StringGetAsync("hold:h1", CommandFlags.None))
           .ReturnsAsync(RedisValue.Null);

        var result = await _cache.GetHoldAsync("h1");

        result.Should().BeNull();
    }

    [Fact]
    public async Task SetHoldAsync_SerializesAndSetsWithSixtySecondTtl()
    {
        var hold = Hold.Create("Alice", [new HoldItem("widget-a", "Widget A", 3)], 15);

        await _cache.SetHoldAsync(hold);

        var inv = _db.Invocations.First(i => i.Method.Name == "StringSetAsync");
        ((string)(RedisKey)inv.Arguments[0]).Should().Be($"hold:{hold.Id}");
        ((Expiration)inv.Arguments[2]).Should().Be((Expiration)TimeSpan.FromSeconds(60));
        var doc = JsonSerializer.Deserialize<JsonElement>((string)(RedisValue)inv.Arguments[1], CamelCase);
        doc.GetProperty("id").GetString().Should().Be(hold.Id);
        doc.GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task InvalidateHoldAsync_DeletesHoldKey()
    {
        _db.Setup(d => d.KeyDeleteAsync("hold:h1", CommandFlags.None)).ReturnsAsync(true);

        await _cache.InvalidateHoldAsync("h1");

        _db.Verify(d => d.KeyDeleteAsync("hold:h1", CommandFlags.None), Times.Once);
    }

    // ── Settings ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetExpirationMinutesAsync_CacheHit_ReturnsParsedInt()
    {
        _db.Setup(d => d.StringGetAsync("settings:expiration-minutes", CommandFlags.None))
           .ReturnsAsync((RedisValue)"15");

        var result = await _cache.GetExpirationMinutesAsync();

        result.Should().Be(15);
    }

    [Fact]
    public async Task SetExpirationMinutesAsync_SetsWithSixtySecondTtl()
    {
        await _cache.SetExpirationMinutesAsync(15);

        var inv = _db.Invocations.First(i => i.Method.Name == "StringSetAsync");
        ((string)(RedisKey)inv.Arguments[0]).Should().Be("settings:expiration-minutes");
        ((string)(RedisValue)inv.Arguments[1]).Should().Be("15");
        ((Expiration)inv.Arguments[2]).Should().Be((Expiration)TimeSpan.FromSeconds(60));
    }

    // ── Flush ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FlushAllAsync_DeletesStaticKeys()
    {
        _db.Setup(d => d.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
           .ReturnsAsync(true);

        await _cache.FlushAllAsync();

        _db.Verify(d => d.KeyDeleteAsync("inventory:all", CommandFlags.None), Times.Once);
        _db.Verify(d => d.KeyDeleteAsync("settings:expiration-minutes", CommandFlags.None), Times.Once);
    }
}
