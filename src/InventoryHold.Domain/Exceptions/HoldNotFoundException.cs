namespace InventoryHold.Domain.Exceptions;

public class HoldNotFoundException : DomainException
{
    public HoldNotFoundException(string holdId)
        : base($"Hold '{holdId}' was not found.") { }
}
