using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InventoryHold.Infrastructure.Persistence.Documents;

[BsonIgnoreExtraElements]
public sealed class AppSettingDocument
{
    [BsonId][BsonRepresentation(BsonType.String)]
    public string Key { get; init; } = "";

    [BsonElement("value")]
    public BsonValue Value { get; init; } = BsonNull.Value;

    [BsonElement("updatedAt")][BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; init; }
}
