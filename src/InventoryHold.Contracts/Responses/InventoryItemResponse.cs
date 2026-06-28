namespace InventoryHold.Contracts.Responses;

public record InventoryItemResponse(
    string ProductId,
    string Name,
    int TotalQuantity,
    int AvailableQuantity,
    int HeldQuantity);
