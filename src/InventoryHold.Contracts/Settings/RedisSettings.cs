namespace InventoryHold.Contracts.Settings;

public record RedisSettings
{
    // StackExchange.Redis format: "host:port" — NOT "redis://host:port"
    public string ConnectionString { get; init; } = "localhost:6379";
}
