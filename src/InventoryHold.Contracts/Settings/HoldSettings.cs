namespace InventoryHold.Contracts.Settings;

public record HoldSettings
{
    public int ExpirationMinutes { get; init; } = 15;
    public int PollingIntervalSeconds { get; init; } = 30;
}
