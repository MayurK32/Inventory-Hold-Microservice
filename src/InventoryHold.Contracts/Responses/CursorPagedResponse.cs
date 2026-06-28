namespace InventoryHold.Contracts.Responses;

public record CursorPagedResponse<T>(
    IReadOnlyList<T> Items,
    string? NextCursor);
