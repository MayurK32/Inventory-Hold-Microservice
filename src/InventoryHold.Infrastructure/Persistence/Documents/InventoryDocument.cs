using InventoryHold.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InventoryHold.Infrastructure.Persistence.Documents;

[BsonIgnoreExtraElements]
public sealed class InventoryDocument
{
    [BsonId]
    public ObjectId Id { get; init; }

    [BsonElement("productId")]
    public string ProductId { get; init; } = "";

    [BsonElement("name")]
    public string Name { get; init; } = "";

    [BsonElement("totalQuantity")]
    public int TotalQuantity { get; init; }

    [BsonElement("availableQuantity")]
    public int AvailableQuantity { get; init; }

    [BsonElement("createdAt")][BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; init; }

    public InventoryItem ToDomain() => new()
    {
        Id = Id.ToString(),
        ProductId = ProductId,
        Name = Name,
        TotalQuantity = TotalQuantity,
        AvailableQuantity = AvailableQuantity,
        CreatedAt = CreatedAt
    };
}
