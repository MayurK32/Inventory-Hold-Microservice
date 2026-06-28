namespace InventoryHold.Domain.Entities;

public class InventoryItem
{
    public string Id { get; set; } = "";
    public string ProductId { get; set; } = "";
    public string Name { get; set; } = "";
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int HeldQuantity => TotalQuantity - AvailableQuantity;
    public DateTime CreatedAt { get; set; }
}
