namespace InventoryHold.Contracts.Events;

public record HoldReleasedEvent(
    string HoldId,
    string? CustomerName,
    string Status,
    List<EventItem> Items,
    DateTime ReleasedAt);
