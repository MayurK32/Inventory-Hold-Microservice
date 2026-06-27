---
name: mongodb-inventory-hold
description: "Project-specific MongoDB skill for the Inventory Hold Microservice. Covers exact collection schemas, C# driver mappings, index definitions, atomic operation patterns, and query plans for all 3 collections."
risk: safe
source: project
date_added: "2026-06-27"
---

# MongoDB — Inventory Hold Microservice

Project-specific skill covering the exact MongoDB schema, C# document mappings, index strategy, and atomic operation patterns for this service. Reference this for all MongoDB implementation — do not deviate from the schemas defined here.

---

## Database Name

```
inventory_hold_db
```

---

## Collections (3 total)

| Collection | Purpose | `_id` type |
|------------|---------|-----------|
| `holds` | Hold lifecycle documents | `string` (GUID) |
| `inventory` | Product stock levels | `ObjectId` |
| `settings` | Runtime configuration | `string` (key) |

---

## Collection: `holds`

### BSON Document Shape

```json
{
  "_id": "550e8400-e29b-41d4-a716-446655440000",
  "customerName": "John Doe",
  "status": "Active",
  "items": [
    { "productId": "widget-a", "productName": "Widget A", "quantity": 2 },
    { "productId": "gadget-x", "productName": "Gadget X", "quantity": 1 }
  ],
  "createdAt": { "$date": "2026-06-27T10:15:00Z" },
  "expiresAt": { "$date": "2026-06-27T10:30:00Z" },
  "releasedAt": null,
  "expiredAt": null
}
```

### Field Definitions

| Field | BSON Type | Nullable | Notes |
|-------|-----------|----------|-------|
| `_id` | string | No | GUID — `Guid.NewGuid().ToString()` — generated before insert |
| `customerName` | string | Yes | Optional display name from POST request |
| `status` | string | No | Enum: `"Active"` \| `"Released"` \| `"Expired"` |
| `items` | array | No | Min 1 item. Embedded — not a reference |
| `items[].productId` | string | No | Slug matching `inventory.productId` |
| `items[].productName` | string | No | Denormalized snapshot at hold creation time |
| `items[].quantity` | int32 | No | ≥ 1 |
| `createdAt` | date | No | `DateTime.UtcNow` at hold creation |
| `expiresAt` | date | No | `createdAt + HoldExpirationMinutes` |
| `releasedAt` | date | Yes | Set by DELETE — never set if expired |
| `expiredAt` | date | Yes | Set by background worker — never set if released |

**Key design choices:**
- `productName` is **denormalized** (snapshot, not FK join) — if a product is renamed later, historical holds preserve the name at time of creation.
- `releasedAt` and `expiredAt` are **separate nullable fields**, not a single `resolvedAt` — allows querying "how many were released vs expired" without additional status check.
- At most one of `releasedAt` / `expiredAt` will ever be set on a single document.

### C# Entity

```csharp
// Domain/Entities/Hold.cs
public sealed class Hold
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [BsonElement("customerName")]
    public string? CustomerName { get; init; }

    [BsonElement("status")]
    [BsonRepresentation(BsonType.String)]
    public HoldStatus Status { get; private set; } = HoldStatus.Active;

    [BsonElement("items")]
    public IReadOnlyList<HoldItem> Items { get; init; } = [];

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    [BsonElement("expiresAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpiresAt { get; init; }

    [BsonElement("releasedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ReleasedAt { get; private set; }

    [BsonElement("expiredAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime? ExpiredAt { get; private set; }

    [BsonIgnoreExtraElements]
    private Hold() { } // required by BSON deserializer

    public static Hold Create(string? customerName, IReadOnlyList<HoldItem> items, DateTime expiresAt)
        => new() { CustomerName = customerName, Items = items, ExpiresAt = expiresAt };

    public void MarkReleased(DateTime at) { Status = HoldStatus.Released; ReleasedAt = at; }
    public void MarkExpired(DateTime at)  { Status = HoldStatus.Expired;  ExpiredAt = at; }
}

public enum HoldStatus { Active, Released, Expired }
```

```csharp
// Domain/Entities/HoldItem.cs
public sealed class HoldItem
{
    [BsonElement("productId")]
    public string ProductId { get; init; } = "";

    [BsonElement("productName")]
    public string ProductName { get; init; } = "";

    [BsonElement("quantity")]
    public int Quantity { get; init; }
}
```

### Indexes

```javascript
// Background worker: find Active holds past their expiresAt
db.holds.createIndex({ status: 1, expiresAt: 1 })

// GET /api/holds list: filter by status, sort newest first
db.holds.createIndex({ status: 1, createdAt: -1 })

// _id index is automatic (covers GET /api/holds/{holdId})
```

```csharp
// Infrastructure/Persistence/HoldCollectionInitializer.cs
var indexModels = new[]
{
    new CreateIndexModel<Hold>(
        Builders<Hold>.IndexKeys.Ascending(h => h.Status).Ascending(h => h.ExpiresAt)),
    new CreateIndexModel<Hold>(
        Builders<Hold>.IndexKeys.Ascending(h => h.Status).Descending(h => h.CreatedAt))
};
await collection.Indexes.CreateManyAsync(indexModels);
```

---

## Collection: `inventory`

### BSON Document Shape

```json
{
  "_id": { "$oid": "6860a1b2c3d4e5f6a7b8c9d0" },
  "productId": "widget-a",
  "name": "Widget A",
  "totalQuantity": 50,
  "availableQuantity": 48,
  "createdAt": { "$date": "2026-06-27T00:00:00Z" }
}
```

### Field Definitions

| Field | BSON Type | Notes |
|-------|-----------|-------|
| `_id` | ObjectId | Internal only — never in API responses or URLs |
| `productId` | string | Public slug (`"widget-a"`) — used in hold items and API |
| `name` | string | Display name (`"Widget A"`) |
| `totalQuantity` | int32 | Set at seed, never changes in this assignment |
| `availableQuantity` | int32 | Materialized. Mutated atomically via `$inc` |
| `createdAt` | date | Seed timestamp |

**`heldQuantity` is NOT stored.** It is computed on read: `totalQuantity - availableQuantity`. This avoids a 3rd field that could drift out of sync.

**`availableQuantity` invariants:**
- `0 ≤ availableQuantity ≤ totalQuantity` — always
- Never decremented below 0 (validated inside the transaction before `$inc`)
- Never incremented above `totalQuantity` (business rule — restore can't exceed total)

### C# Entity

```csharp
// Domain/Entities/InventoryItem.cs
[BsonIgnoreExtraElements]
public sealed class InventoryItem
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

    [BsonElement("createdAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CreatedAt { get; init; }

    // Computed — not stored in MongoDB
    [BsonIgnore]
    public int HeldQuantity => TotalQuantity - AvailableQuantity;
}
```

### Indexes

```javascript
// Unique index on productId — fast lookup and prevents duplicates
db.inventory.createIndex({ productId: 1 }, { unique: true })
```

```csharp
// Infrastructure/Persistence/InventoryCollectionInitializer.cs
await collection.Indexes.CreateOneAsync(
    new CreateIndexModel<InventoryItem>(
        Builders<InventoryItem>.IndexKeys.Ascending(i => i.ProductId),
        new CreateIndexOptions { Unique = true }));
```

### Seed Data

```csharp
// Infrastructure/Persistence/DatabaseSeeder.cs
private static readonly InventoryItem[] SeedItems =
[
    new() { ProductId = "widget-a", Name = "Widget A",      TotalQuantity = 50,  AvailableQuantity = 50,  CreatedAt = DateTime.UtcNow },
    new() { ProductId = "widget-b", Name = "Widget B",      TotalQuantity = 30,  AvailableQuantity = 30,  CreatedAt = DateTime.UtcNow },
    new() { ProductId = "gadget-x", Name = "Gadget X",      TotalQuantity = 20,  AvailableQuantity = 20,  CreatedAt = DateTime.UtcNow },
    new() { ProductId = "device-z", Name = "Device Z",      TotalQuantity = 10,  AvailableQuantity = 10,  CreatedAt = DateTime.UtcNow },
    new() { ProductId = "part-001", Name = "Spare Part 001", TotalQuantity = 100, AvailableQuantity = 100, CreatedAt = DateTime.UtcNow }
];

// Seed only if inventory is empty (startup check)
var count = await _inventory.CountDocumentsAsync(FilterDefinition<InventoryItem>.Empty);
if (count == 0)
    await _inventory.InsertManyAsync(SeedItems);
```

---

## Collection: `settings`

### BSON Document Shape

```json
{ "_id": "HoldExpirationMinutes", "value": 15, "updatedAt": { "$date": "2026-06-27T00:00:00Z" } }
```

### Field Definitions

| Field | BSON Type | Notes |
|-------|-----------|-------|
| `_id` | string | Key name (e.g., `"HoldExpirationMinutes"`) |
| `value` | BsonValue | Heterogeneous — int, string, bool depending on setting |
| `updatedAt` | date | Last modified time |

### C# Entity

```csharp
// Domain/Entities/AppSetting.cs
[BsonIgnoreExtraElements]
public sealed class AppSetting
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string Key { get; init; } = "";

    [BsonElement("value")]
    public BsonValue Value { get; init; } = BsonNull.Value;

    [BsonElement("updatedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime UpdatedAt { get; init; }
}
```

```csharp
// Reading the setting with int fallback
var setting = await _settings.Find(s => s.Key == "HoldExpirationMinutes").FirstOrDefaultAsync();
var minutes = setting?.Value.AsNullableInt32 ?? _defaultSettings.ExpirationMinutes;
```

---

## Atomic Operation Patterns

### POST /api/holds — Multi-Document Transaction

```csharp
using var session = await _client.StartSessionAsync();
await session.WithTransactionAsync(async (s, ct) =>
{
    // Phase 1: Validate ALL items have sufficient stock
    var failures = new List<StockFailure>();
    var inventorySnapshots = new List<(HoldItemRequest item, InventoryItem inv)>();

    foreach (var item in request.Items)
    {
        var inv = await _inventory
            .Find(s, i => i.ProductId == item.ProductId)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(item.ProductId);

        if (inv.AvailableQuantity < item.Quantity)
            failures.Add(new(item.ProductId, item.Quantity, inv.AvailableQuantity));
        else
            inventorySnapshots.Add((item, inv));
    }

    if (failures.Count > 0)
        throw new InsufficientStockException(failures); // → 409

    // Phase 2: All passed — decrement and create hold atomically
    foreach (var (item, _) in inventorySnapshots)
    {
        await _inventory.UpdateOneAsync(s,
            i => i.ProductId == item.ProductId,
            Builders<InventoryItem>.Update.Inc(i => i.AvailableQuantity, -item.Quantity),
            cancellationToken: ct);
    }

    var hold = Hold.Create(request.CustomerName,
        inventorySnapshots.Select(x => new HoldItem
        {
            ProductId = x.item.ProductId,
            ProductName = x.inv.Name,      // snapshot name from inventory
            Quantity = x.item.Quantity
        }).ToList(),
        DateTime.UtcNow.AddMinutes(expirationMinutes));

    await _holds.InsertOneAsync(s, hold, cancellationToken: ct);
    return hold;
}, cancellationToken: cancellationToken);
```

**Write conflict (MongoCommandException code 112):** Retry this entire block up to 3× with 50ms exponential backoff. → See `error-handling-patterns` skill.

### DELETE /api/holds/{holdId} — Atomic Status Transition

```csharp
// Step 1: Atomic transition (guard: only matches Active holds)
var releasedHold = await _holds.FindOneAndUpdateAsync(
    h => h.Id == holdId && h.Status == HoldStatus.Active,
    Builders<Hold>.Update
        .Set(h => h.Status, HoldStatus.Released)
        .Set(h => h.ReleasedAt, DateTime.UtcNow),
    new FindOneAndUpdateOptions<Hold> { ReturnDocument = ReturnDocument.After });

if (releasedHold is null)
{
    // Didn't match — check why
    var existing = await _holds.Find(h => h.Id == holdId).FirstOrDefaultAsync();
    return existing is null
        ? Results.NotFound()           // 404 — never existed
        : Results.Gone(existing);      // 410 — exists but terminal state
}

// Step 2: Restore inventory (only reached if we won the atomic race)
foreach (var item in releasedHold.Items)
{
    await _inventory.UpdateOneAsync(
        i => i.ProductId == item.ProductId,
        Builders<InventoryItem>.Update.Inc(i => i.AvailableQuantity, item.Quantity));
}

// Step 3: Publish event (fire-and-forget)
// Step 4: Invalidate Redis cache
```

### Background Worker — Expiry Batch

```csharp
// Find candidates (uses { status: 1, expiresAt: 1 } index)
var expired = await _holds
    .Find(h => h.Status == HoldStatus.Active && h.ExpiresAt <= DateTime.UtcNow)
    .ToListAsync();

int actuallyExpired = 0;

foreach (var hold in expired)
{
    // Atomic guard — handles race with concurrent DELETE
    var result = await _holds.FindOneAndUpdateAsync(
        h => h.Id == hold.Id && h.Status == HoldStatus.Active,
        Builders<Hold>.Update
            .Set(h => h.Status, HoldStatus.Expired)
            .Set(h => h.ExpiredAt, DateTime.UtcNow),
        new FindOneAndUpdateOptions<Hold> { ReturnDocument = ReturnDocument.After });

    if (result is null) continue; // lost the race — DELETE already handled it

    foreach (var item in result.Items)
        await _inventory.UpdateOneAsync(
            i => i.ProductId == item.ProductId,
            Builders<InventoryItem>.Update.Inc(i => i.AvailableQuantity, item.Quantity));

    actuallyExpired++;
    // Publish HoldExpired event (fire-and-forget)
}

// Only invalidate Redis if something actually changed
if (actuallyExpired > 0)
    await _cache.InvalidateInventoryAsync();
```

### POST /api/inventory/reset — Full Reset

```csharp
// 1. Delete all holds
await _holds.DeleteManyAsync(FilterDefinition<Hold>.Empty);

// 2. Restore all inventory to totalQuantity (no transaction needed — not business critical)
foreach (var seed in SeedItems)
{
    await _inventory.UpdateOneAsync(
        i => i.ProductId == seed.ProductId,
        Builders<InventoryItem>.Update.Set(i => i.AvailableQuantity, seed.TotalQuantity));
}

// 3. Flush all Redis cache
await _cache.FlushAllAsync();
```

---

## Query Patterns

| Operation | Collection | Filter | Index Used |
|-----------|------------|--------|-----------|
| Get single hold | `holds` | `{ _id: holdId }` | default `_id` |
| List holds (active only) | `holds` | `{ status: "Active" }` + sort `createdAt: -1` | `{status, createdAt}` |
| List holds (all statuses) | `holds` | `{}` + sort `createdAt: -1` | collection scan (small dataset) |
| Background worker candidates | `holds` | `{ status: "Active", expiresAt: { $lte: now } }` | `{status, expiresAt}` |
| Get inventory item by slug | `inventory` | `{ productId: "widget-a" }` | `{productId}` unique |
| Get all inventory | `inventory` | `{}` | collection scan (5 items) |
| Get setting | `settings` | `{ _id: "HoldExpirationMinutes" }` | default `_id` |

---

## Configuration

### appsettings.json
```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://localhost:27017"
  },
  "MongoDB": {
    "DatabaseName": "inventory_hold_db"
  }
}
```

### Docker Compose
```yaml
mongodb:
  image: mongo:7
  ports:
    - "27017:27017"
  volumes:
    - mongo_data:/data/db
  healthcheck:
    test: ["CMD", "mongosh", "--eval", "db.adminCommand('ping')"]
    interval: 10s
    timeout: 5s
    retries: 5

api:
  environment:
    - ConnectionStrings__MongoDB=mongodb://mongodb:27017
    - MongoDB__DatabaseName=inventory_hold_db
  depends_on:
    mongodb:
      condition: service_healthy
```

### DI Registration
```csharp
// Program.cs
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDB"));
builder.Services.AddSingleton<IMongoClient>(sp =>
    new MongoClient(builder.Configuration.GetConnectionString("MongoDB")));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>()
      .GetDatabase(sp.GetRequiredService<IOptions<MongoDbSettings>>().Value.DatabaseName));

// Collections
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoDatabase>().GetCollection<Hold>("holds"));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoDatabase>().GetCollection<InventoryItem>("inventory"));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoDatabase>().GetCollection<AppSetting>("settings"));
```

### Health Check
```csharp
builder.Services.AddHealthChecks()
    .AddMongoDb(
        sp => sp.GetRequiredService<IMongoClient>(),
        name: "mongodb",
        timeout: TimeSpan.FromSeconds(3));
```

---

## Key Invariants (Never Violate)

1. `availableQuantity >= 0` — validate inside transaction before `$inc -N`
2. `availableQuantity <= totalQuantity` — restore can never exceed total
3. Hold `status` transitions are **one-way**: Active → Released OR Active → Expired (never reversed)
4. `findOneAndUpdate` with `status: "Active"` guard on all status transitions — prevents race conditions
5. All `DateTime` values stored as UTC — use `DateTime.UtcNow` everywhere, never `DateTime.Now`
6. `_id` on holds is generated **before** the MongoDB insert — allows idempotency and event payload population without a round-trip
7. `productName` in hold items is **snapshotted at creation time** — never updated retroactively

## Limitations
- This skill is scoped to this project's specific MongoDB schema — do not generalize.
- Schema changes require updating this skill and the relevant C# entities together.
