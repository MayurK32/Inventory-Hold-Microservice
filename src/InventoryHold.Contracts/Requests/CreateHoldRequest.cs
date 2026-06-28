namespace InventoryHold.Contracts.Requests;

public record CreateHoldItemRequest(string ProductId, int Quantity);
public record CreateHoldRequest(string? CustomerName, IReadOnlyList<CreateHoldItemRequest> Items);
