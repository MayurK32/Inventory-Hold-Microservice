namespace InventoryHold.Domain.Entities;

public class AppSetting
{
    public string Id { get; set; } = "";
    public object? Value { get; set; }
    public DateTime UpdatedAt { get; set; }
}
