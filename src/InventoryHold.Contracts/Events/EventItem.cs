namespace InventoryHold.Contracts.Events;

public record EventItem(string ProductId, string ProductName, int Quantity);
