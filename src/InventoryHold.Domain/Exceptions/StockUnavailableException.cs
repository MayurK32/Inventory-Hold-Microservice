namespace InventoryHold.Domain.Exceptions;

public class StockUnavailableException()
    : DomainException("Stock temporarily unavailable. Please try again.");
