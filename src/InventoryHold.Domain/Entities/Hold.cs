using InventoryHold.Domain.Exceptions;

namespace InventoryHold.Domain.Entities;

public class Hold
{
    private Hold() { }

    public string Id { get; private set; } = "";
    public string? CustomerName { get; private set; }
    public HoldStatus Status { get; private set; }
    public IReadOnlyList<HoldItem> Items { get; private set; } = [];
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ReleasedAt { get; private set; }
    public DateTime? ExpiredAt { get; private set; }

    public static Hold Create(string? customerName, IReadOnlyList<HoldItem> items, int expirationMinutes)
    {
        if (items == null || items.Count == 0)
            throw new DomainException("Hold must have at least one item.");

        var now = DateTime.UtcNow;
        return new Hold
        {
            Id = Guid.NewGuid().ToString(),
            CustomerName = customerName,
            Status = HoldStatus.Active,
            Items = items,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(expirationMinutes)
        };
    }

    public void MarkReleased()
    {
        if (Status != HoldStatus.Active)
            throw new HoldTerminatedException(Id, Status, Status == HoldStatus.Released ? ReleasedAt : ExpiredAt);

        Status = HoldStatus.Released;
        ReleasedAt = DateTime.UtcNow;
    }

    public void MarkExpired()
    {
        if (Status != HoldStatus.Active)
            throw new HoldTerminatedException(Id, Status, Status == HoldStatus.Released ? ReleasedAt : ExpiredAt);

        Status = HoldStatus.Expired;
        ExpiredAt = DateTime.UtcNow;
    }

    public static Hold Reconstruct(
        string id, string? customerName, HoldStatus status,
        IReadOnlyList<HoldItem> items, DateTime createdAt, DateTime expiresAt,
        DateTime? releasedAt, DateTime? expiredAt) => new Hold
    {
        Id = id,
        CustomerName = customerName,
        Status = status,
        Items = items,
        CreatedAt = createdAt,
        ExpiresAt = expiresAt,
        ReleasedAt = releasedAt,
        ExpiredAt = expiredAt
    };
}
