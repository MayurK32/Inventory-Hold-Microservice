namespace InventoryHold.Contracts.Events;

public record HoldCreatedEvent(
    string HoldId,
    string? CustomerName,
    string Status,
    List<EventItem> Items,
    DateTime CreatedAt,
    DateTime ExpiresAt);
