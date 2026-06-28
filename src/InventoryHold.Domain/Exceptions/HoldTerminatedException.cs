using InventoryHold.Domain.Entities;

namespace InventoryHold.Domain.Exceptions;

public class HoldTerminatedException : DomainException
{
    public HoldStatus Status { get; }
    public DateTime? At { get; }

    public HoldTerminatedException(string holdId, HoldStatus status, DateTime? at)
        : base($"Hold '{holdId}' is already {status}.")
    {
        Status = status;
        At = at;
    }
}
