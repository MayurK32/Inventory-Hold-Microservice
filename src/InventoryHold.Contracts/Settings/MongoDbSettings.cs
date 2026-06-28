namespace InventoryHold.Contracts.Settings;

public record MongoDbSettings
{
    public string ConnectionString { get; init; } = "mongodb://localhost:27017";
    public string DatabaseName { get; init; } = "inventory_hold_db";
}
