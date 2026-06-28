namespace InventoryHold.Domain.Exceptions;

public record StockFailure(string ProductId, int Requested, int Available);

public class InsufficientStockException : DomainException
{
    public IReadOnlyList<StockFailure> Failures { get; }

    public InsufficientStockException(IReadOnlyList<StockFailure> failures)
        : base("One or more items have insufficient stock.")
        => Failures = failures;
}
