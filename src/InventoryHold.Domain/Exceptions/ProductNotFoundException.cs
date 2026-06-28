namespace InventoryHold.Domain.Exceptions;

public class ProductNotFoundException(string productId)
    : DomainException($"Product '{productId}' not found in inventory.");
