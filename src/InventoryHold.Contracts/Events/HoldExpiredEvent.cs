namespace InventoryHold.Contracts.Events;

public record HoldExpiredEvent(
    string HoldId,
    string? CustomerName,
    string Status,
    List<EventItem> Items,
    DateTime ExpiredAt);
