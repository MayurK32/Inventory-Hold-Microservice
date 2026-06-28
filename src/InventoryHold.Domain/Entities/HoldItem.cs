using InventoryHold.Domain.Exceptions;

namespace InventoryHold.Domain.Entities;

public record HoldItem
{
    public string ProductId { get; init; }
    public string ProductName { get; init; }
    public int Quantity { get; init; }

    public HoldItem(string productId, string productName, int quantity)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new DomainException("ProductId cannot be empty.");
        if (string.IsNullOrWhiteSpace(productName))
            throw new DomainException("ProductName cannot be empty.");
        if (quantity < 1)
            throw new DomainException("Quantity must be at least 1.");

        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
    }
}
