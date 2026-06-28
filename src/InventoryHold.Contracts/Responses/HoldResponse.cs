namespace InventoryHold.Contracts.Responses;

public record HoldItemResponse(string ProductId, string ProductName, int Quantity);
public record HoldResponse(
    string HoldId,
    string? CustomerName,
    string Status,
    IReadOnlyList<HoldItemResponse> Items,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? ReleasedAt,
    DateTime? ExpiredAt);
