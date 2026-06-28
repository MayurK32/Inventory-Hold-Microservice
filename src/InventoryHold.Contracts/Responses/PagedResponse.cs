namespace InventoryHold.Contracts.Responses;

public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    long Total,
    int Page,
    int PageSize,
    int TotalPages);
