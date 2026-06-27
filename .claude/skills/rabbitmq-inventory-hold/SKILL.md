---
name: rabbitmq-inventory-hold
description: "Project-specific RabbitMQ skill for the Inventory Hold Microservice. Covers the exact exchange/queue topology, event payload schemas, fire-and-forget publisher pattern, and .NET 10 implementation for hold lifecycle events."
risk: safe
source: project
date_added: "2026-06-27"
---

# RabbitMQ — Inventory Hold Microservice

Project-specific skill encapsulating every RabbitMQ decision made for this service. Reference this skill for all messaging implementation work — do not deviate from the topology, payloads, or patterns defined here.

---

## Scope

**Publishers only.** This service publishes 3 event types. It does NOT implement any consumers — downstream services consume these events independently.

---

## Topology (Decided — Do Not Change)

```
Exchange: inventory.hold.events
Type:     direct
Durable:  true

Bindings:
  routing key: hold.created  → Queue: hold.created.queue  (durable)
  routing key: hold.released → Queue: hold.released.queue (durable)
  routing key: hold.expired  → Queue: hold.expired.queue  (durable)
```

Declare the exchange and all 3 queues on application startup (idempotent — `passive: false`, `durable: true`, `autoDelete: false`). If they already exist with matching settings, declaration is a no-op.

---

## Event Payload Schemas (Decided — Do Not Change)

All events are serialized as **UTF-8 JSON**. Content-type: `application/json`.

### HoldCreated
Routing key: `hold.created`
```json
{
  "holdId": "550e8400-e29b-41d4-a716-446655440000",
  "customerName": "John Doe",
  "status": "Active",
  "items": [
    { "productId": "widget-a", "productName": "Widget A", "quantity": 2 },
    { "productId": "gadget-x", "productName": "Gadget X", "quantity": 1 }
  ],
  "createdAt": "2026-06-27T10:15:00Z",
  "expiresAt": "2026-06-27T10:30:00Z"
}
```

### HoldReleased
Routing key: `hold.released`
```json
{
  "holdId": "550e8400-e29b-41d4-a716-446655440000",
  "customerName": "John Doe",
  "status": "Released",
  "items": [
    { "productId": "widget-a", "productName": "Widget A", "quantity": 2 },
    { "productId": "gadget-x", "productName": "Gadget X", "quantity": 1 }
  ],
  "releasedAt": "2026-06-27T10:20:00Z"
}
```

### HoldExpired
Routing key: `hold.expired`
```json
{
  "holdId": "550e8400-e29b-41d4-a716-446655440000",
  "customerName": "John Doe",
  "status": "Expired",
  "items": [
    { "productId": "widget-a", "productName": "Widget A", "quantity": 2 },
    { "productId": "gadget-x", "productName": "Gadget X", "quantity": 1 }
  ],
  "expiredAt": "2026-06-27T10:30:00Z"
}
```

**Why `items` in every event:** A downstream restocking or analytics service can act without a secondary DB lookup.

---

## When Each Event Is Published

| Event | Trigger | Publisher Location |
|-------|---------|-------------------|
| `HoldCreated` | After `POST /api/holds` MongoDB transaction commits successfully | `HoldsController.CreateHold` or `HoldService.CreateHoldAsync` |
| `HoldReleased` | After `DELETE /api/holds/{holdId}` atomically transitions Active → Released | `HoldsController.ReleaseHold` or `HoldService.ReleaseHoldAsync` |
| `HoldExpired` | After background worker atomically transitions Active → Expired per hold | `HoldExpiryWorker` (IHostedService) |

**Critical:** Never publish before the MongoDB write commits. Publish after the DB operation is confirmed successful.

---

## Publish Failure Handling (Decided — Do Not Change)

**Fire-and-forget with logging.** If RabbitMQ publish throws, log the error at `LogLevel.Error` and continue. The hold DB operation is already committed and must NOT be rolled back.

```csharp
try
{
    await _publisher.PublishHoldCreatedAsync(hold);
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to publish HoldCreated event for hold {HoldId}", hold.Id);
    // Do NOT throw — the hold is already saved, client gets 201 regardless
}
```

**Documented trade-off:** Production-grade approach is the Transactional Outbox Pattern — persist the event to a MongoDB outbox collection inside the same transaction, separate worker publishes and marks as sent. Guarantees at-least-once delivery. Out of scope for this assignment.

---

## .NET 10 Implementation

### Package
```xml
<PackageReference Include="RabbitMQ.Client" Version="7.*" />
```

### Connection Management
Use a single long-lived `IConnection` (managed as a singleton). Create one `IChannel` per publish operation or use a channel pool — channels are lightweight.

```csharp
// Infrastructure/Messaging/RabbitMqConnectionFactory.cs
public sealed class RabbitMqConnectionFactory : IAsyncDisposable
{
    private readonly IConnection _connection;

    public static async Task<RabbitMqConnectionFactory> CreateAsync(RabbitMqSettings settings)
    {
        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            UserName = settings.Username,
            Password = settings.Password,
            VirtualHost = settings.VirtualHost,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };
        var connection = await factory.CreateConnectionAsync();
        return new RabbitMqConnectionFactory(connection);
    }

    private RabbitMqConnectionFactory(IConnection connection) => _connection = connection;

    public IConnection Connection => _connection;

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}
```

### Topology Declaration (Startup)
```csharp
// Infrastructure/Messaging/RabbitMqTopologyInitializer.cs
public static class RabbitMqTopologyInitializer
{
    public const string Exchange = "inventory.hold.events";
    public const string HoldCreatedQueue = "hold.created.queue";
    public const string HoldReleasedQueue = "hold.released.queue";
    public const string HoldExpiredQueue = "hold.expired.queue";
    public const string RoutingKeyCreated = "hold.created";
    public const string RoutingKeyReleased = "hold.released";
    public const string RoutingKeyExpired = "hold.expired";

    public static async Task DeclareAsync(IConnection connection)
    {
        await using var channel = await connection.CreateChannelAsync();

        // Declare exchange
        await channel.ExchangeDeclareAsync(
            exchange: Exchange,
            type: ExchangeType.Direct,
            durable: true,
            autoDelete: false);

        // Declare queues and bind
        foreach (var (queue, routingKey) in new[]
        {
            (HoldCreatedQueue, RoutingKeyCreated),
            (HoldReleasedQueue, RoutingKeyReleased),
            (HoldExpiredQueue, RoutingKeyExpired)
        })
        {
            await channel.QueueDeclareAsync(
                queue: queue,
                durable: true,
                exclusive: false,
                autoDelete: false);

            await channel.QueueBindAsync(
                queue: queue,
                exchange: Exchange,
                routingKey: routingKey);
        }
    }
}
```

### Publisher Interface
```csharp
// Domain/Messaging/IHoldEventPublisher.cs
public interface IHoldEventPublisher
{
    Task PublishHoldCreatedAsync(Hold hold, CancellationToken ct = default);
    Task PublishHoldReleasedAsync(Hold hold, CancellationToken ct = default);
    Task PublishHoldExpiredAsync(Hold hold, CancellationToken ct = default);
}
```

### Publisher Implementation
```csharp
// Infrastructure/Messaging/RabbitMqHoldEventPublisher.cs
public sealed class RabbitMqHoldEventPublisher : IHoldEventPublisher
{
    private readonly IConnection _connection;
    private readonly ILogger<RabbitMqHoldEventPublisher> _logger;

    public RabbitMqHoldEventPublisher(RabbitMqConnectionFactory factory,
        ILogger<RabbitMqHoldEventPublisher> logger)
    {
        _connection = factory.Connection;
        _logger = logger;
    }

    public Task PublishHoldCreatedAsync(Hold hold, CancellationToken ct = default)
    {
        var payload = new HoldCreatedEvent
        {
            HoldId = hold.Id,
            CustomerName = hold.CustomerName,
            Status = hold.Status.ToString(),
            Items = hold.Items.Select(i => new EventItem(i.ProductId, i.ProductName, i.Quantity)).ToList(),
            CreatedAt = hold.CreatedAt,
            ExpiresAt = hold.ExpiresAt
        };
        return PublishAsync(RabbitMqTopologyInitializer.RoutingKeyCreated, payload, ct);
    }

    public Task PublishHoldReleasedAsync(Hold hold, CancellationToken ct = default)
    {
        var payload = new HoldReleasedEvent
        {
            HoldId = hold.Id,
            CustomerName = hold.CustomerName,
            Status = hold.Status.ToString(),
            Items = hold.Items.Select(i => new EventItem(i.ProductId, i.ProductName, i.Quantity)).ToList(),
            ReleasedAt = hold.ReleasedAt!.Value
        };
        return PublishAsync(RabbitMqTopologyInitializer.RoutingKeyReleased, payload, ct);
    }

    public Task PublishHoldExpiredAsync(Hold hold, CancellationToken ct = default)
    {
        var payload = new HoldExpiredEvent
        {
            HoldId = hold.Id,
            CustomerName = hold.CustomerName,
            Status = hold.Status.ToString(),
            Items = hold.Items.Select(i => new EventItem(i.ProductId, i.ProductName, i.Quantity)).ToList(),
            ExpiredAt = hold.ExpiredAt!.Value
        };
        return PublishAsync(RabbitMqTopologyInitializer.RoutingKeyExpired, payload, ct);
    }

    private async Task PublishAsync<T>(string routingKey, T payload, CancellationToken ct)
    {
        await using var channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        var body = JsonSerializer.SerializeToUtf8Bytes(payload, JsonSerializerOptions.Web);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = Guid.NewGuid().ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(
            exchange: RabbitMqTopologyInitializer.Exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);

        _logger.LogInformation("Published {RoutingKey} event", routingKey);
    }
}
```

### Event DTO Records
```csharp
// Infrastructure/Messaging/Events/
public record EventItem(string ProductId, string ProductName, int Quantity);

public record HoldCreatedEvent
{
    public string HoldId { get; init; } = "";
    public string? CustomerName { get; init; }
    public string Status { get; init; } = "";
    public List<EventItem> Items { get; init; } = [];
    public DateTime CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
}

public record HoldReleasedEvent
{
    public string HoldId { get; init; } = "";
    public string? CustomerName { get; init; }
    public string Status { get; init; } = "";
    public List<EventItem> Items { get; init; } = [];
    public DateTime ReleasedAt { get; init; }
}

public record HoldExpiredEvent
{
    public string HoldId { get; init; } = "";
    public string? CustomerName { get; init; }
    public string Status { get; init; } = "";
    public List<EventItem> Items { get; init; } = [];
    public DateTime ExpiredAt { get; init; }
}
```

### DI Registration (Program.cs)
```csharp
// Register RabbitMQ
builder.Services.AddSingleton<RabbitMqConnectionFactory>(sp =>
    RabbitMqConnectionFactory.CreateAsync(
        sp.GetRequiredService<IOptions<RabbitMqSettings>>().Value
    ).GetAwaiter().GetResult());

builder.Services.AddScoped<IHoldEventPublisher, RabbitMqHoldEventPublisher>();

// Declare topology at startup (after app is built)
var rabbitFactory = app.Services.GetRequiredService<RabbitMqConnectionFactory>();
await RabbitMqTopologyInitializer.DeclareAsync(rabbitFactory.Connection);
```

---

## Configuration

### appsettings.json
```json
{
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "VirtualHost": "/"
  }
}
```

### docker-compose environment
```yaml
rabbitmq:
  image: rabbitmq:3-management
  ports:
    - "5672:5672"
    - "15672:15672"
  healthcheck:
    test: ["CMD", "rabbitmq-diagnostics", "ping"]
    interval: 10s
    timeout: 5s
    retries: 5

api:
  environment:
    - RabbitMQ__Host=rabbitmq
    - RabbitMQ__Port=5672
    - RabbitMQ__Username=guest
    - RabbitMQ__Password=guest
  depends_on:
    rabbitmq:
      condition: service_healthy
```

### Settings Record
```csharp
public record RabbitMqSettings
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string Username { get; init; } = "guest";
    public string Password { get; init; } = "guest";
    public string VirtualHost { get; init; } = "/";
}
```

---

## Health Check

```csharp
// Infrastructure/HealthChecks/RabbitMqHealthCheck.cs
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqConnectionFactory _factory;

    public RabbitMqHealthCheck(RabbitMqConnectionFactory factory) => _factory = factory;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = _factory.Connection.IsOpen
            ? HealthCheckResult.Healthy("RabbitMQ connection is open.")
            : HealthCheckResult.Unhealthy("RabbitMQ connection is closed.");
        return Task.FromResult(result);
    }
}

// Registration in Program.cs
builder.Services.AddHealthChecks()
    .AddCheck<RabbitMqHealthCheck>("rabbitmq");
```

---

## Unit Testing (Mock Publisher)

```csharp
// In unit tests, register a no-op publisher
public sealed class NoOpHoldEventPublisher : IHoldEventPublisher
{
    public Task PublishHoldCreatedAsync(Hold hold, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishHoldReleasedAsync(Hold hold, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishHoldExpiredAsync(Hold hold, CancellationToken ct = default) => Task.CompletedTask;
}

// Verify publishing was called (with Moq)
var publisherMock = new Mock<IHoldEventPublisher>();
publisherMock.Verify(p => p.PublishHoldCreatedAsync(
    It.Is<Hold>(h => h.Id == expectedHoldId), It.IsAny<CancellationToken>()), Times.Once);
```

---

## Key Constraints

1. **Never publish before MongoDB commit** — event must reflect committed state
2. **Never block HTTP response on publish** — wrap in try/catch, log and continue
3. **Always use persistent delivery mode** — messages survive RabbitMQ restart
4. **Always use durable queues** — queues survive RabbitMQ restart
5. **topology declaration is idempotent** — safe to call on every startup
6. **`customerName` may be null** — serialize as `null`, not omitted
7. **All timestamps are UTC** — use `DateTime.UtcNow`, serialize as ISO 8601 (`Z` suffix)

## Limitations
- This skill is scoped to this project's specific RabbitMQ requirements — do not generalize.
- No consumer implementation. No outbox pattern. Fire-and-forget only.
- For production-grade delivery guarantees, the Transactional Outbox Pattern is the correct upgrade path.
