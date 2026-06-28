using InventoryHold.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InventoryHold.Infrastructure.Persistence.Documents;

[BsonIgnoreExtraElements]
public sealed class HoldDocument
{
    [BsonId][BsonRepresentation(BsonType.String)]
    public string Id { get; init; } = "";

    [BsonElement("customerName")]
    public string? CustomerName { get; init; }

    [BsonElement("status")][BsonRepresentation(BsonType.String)]
    public HoldStatus Status { get; init; }

    [BsonElement("items")]
    public List<HoldItemDocument> Items { get; init; } = [];

    [BsonElement("createdAt")][BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; init; }

    [BsonElement("expiresAt")][BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAt { get; init; }

    [BsonElement("releasedAt")][BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ReleasedAt { get; init; }

    [BsonElement("expiredAt")][BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ExpiredAt { get; init; }

    public Hold ToDomain() => Hold.Reconstruct(
        Id, CustomerName, Status,
        Items.Select(i => new HoldItem(i.ProductId, i.ProductName, i.Quantity)).ToList(),
        CreatedAt, ExpiresAt, ReleasedAt, ExpiredAt);

    public static HoldDocument FromDomain(Hold h) => new()
    {
        Id = h.Id,
        CustomerName = h.CustomerName,
        Status = h.Status,
        Items = h.Items.Select(HoldItemDocument.FromDomain).ToList(),
        CreatedAt = h.CreatedAt,
        ExpiresAt = h.ExpiresAt,
        ReleasedAt = h.ReleasedAt,
        ExpiredAt = h.ExpiredAt
    };
}

public sealed class HoldItemDocument
{
    [BsonElement("productId")]   public string ProductId { get; init; } = "";
    [BsonElement("productName")] public string ProductName { get; init; } = "";
    [BsonElement("quantity")]    public int Quantity { get; init; }

    public static HoldItemDocument FromDomain(HoldItem i) =>
        new() { ProductId = i.ProductId, ProductName = i.ProductName, Quantity = i.Quantity };
}
