# Inventory Hold Microservice

A production-quality inventory reservation service built with .NET 10, MongoDB, Redis, RabbitMQ, and React — demonstrating AI-augmented full-stack development for the Kibo Commerce senior engineer assignment.

---

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (v4.x+)
- No other local dependencies required — everything runs in containers

---

## One-Command Startup

```bash
docker-compose up --build
```

All 5 services start in dependency order. First run pulls images and compiles the .NET project (~2–3 minutes). Subsequent runs are faster.

| Service | URL | Notes |
|---------|-----|-------|
| React Frontend | http://localhost:3000 | Inventory dashboard, create/release holds |
| API (via nginx) | http://localhost:3000/api | All REST endpoints |
| Health check | http://localhost:3000/health | `{"status":"Healthy"}` when all dependencies up |
| API docs (Scalar) | http://localhost:3000/api/scalar/v1 | Interactive OpenAPI UI |
| RabbitMQ Management | http://localhost:15672 | guest / guest |
| MongoDB | localhost:27017 | inventory\_hold\_db |
| Redis | localhost:6379 | |

> Port 80 is restricted on Windows without admin elevation; the frontend is mapped to **3000:80**.

---

## Running Tests

```bash
dotnet test src/InventoryHold.UnitTests/InventoryHold.UnitTests.csproj
```

82 tests, all passing. Zero tests require running infrastructure — all dependencies mocked via Moq.

---

## API Reference

### POST /api/holds
Place a hold on one or more inventory items.

```json
{
  "customerName": "Alice",
  "items": [
    { "productId": "widget-a", "quantity": 5 }
  ]
}
```

| Status | Condition |
|--------|-----------|
| 201 Created | Hold placed, inventory decremented |
| 404 Not Found | productId does not exist |
| 409 Conflict | Insufficient stock — body includes `failures[]` with per-product breakdown |
| 422 Unprocessable | Validation error (empty items, qty ≤ 0) |

### GET /api/holds/{holdId}
Retrieve a hold by ID. Returns the hold in any state (Active / Released / Expired). Returns 404 for unknown IDs.

### DELETE /api/holds/{holdId}
Release an active hold and restore inventory.

| Status | Condition |
|--------|-----------|
| 200 OK | Released, `releasedAt` populated |
| 404 Not Found | Hold ID never existed |
| 410 Gone | Hold already Released or Expired — body includes `data.at` timestamp |

### GET /api/holds
List holds with optional filtering.

| Query param | Default | Notes |
|-------------|---------|-------|
| `status` | *(omit = all)* | `active`, `released`, `expired` |
| `page` | 1 | |
| `pageSize` | 20 | Max 100 |

### GET /api/inventory
Returns all 5 seed products with `totalQuantity`, `availableQuantity`, `heldQuantity`.

### POST /api/inventory/reset
Resets all inventory to seed quantities and deletes all holds. Useful for demos.

---

## Architecture

```
src/
├── InventoryHold.Contracts/        # DTOs, enums, request/response/event models
├── InventoryHold.Domain/
│   ├── Entities/                   # Hold, InventoryItem, HoldItem, AppSetting
│   ├── Exceptions/                 # Typed domain exceptions
│   ├── Repositories/               # IHoldRepository, IInventoryRepository, ISettingsRepository
│   ├── Messaging/                  # IHoldEventPublisher
│   ├── Cache/                      # IInventoryCache
│   └── Transactions/               # IMongoTransaction, ITransactionFactory
├── InventoryHold.Infrastructure/   # MongoDB, Redis, RabbitMQ implementations
├── InventoryHold.WebApi/
│   ├── Endpoints/                  # Minimal API endpoint mappers
│   ├── Services/                   # HoldService, InventoryService (application layer)
│   ├── Workers/                    # HoldExpiryWorker (BackgroundService)
│   ├── HealthChecks/               # RabbitMqHealthCheck
│   └── Middleware/                 # DomainExceptionHandler → RFC 7807 ProblemDetails
└── InventoryHold.UnitTests/        # 82 unit tests
```

**Key architectural decisions:**

- **Services in WebApi/Services/** — Application services live in the WebApi layer rather than Domain/Services. Domain stays pure (entities, exceptions, interfaces only); the service layer orchestrates between domain and infrastructure. This keeps the Domain project infrastructure-free and independently testable.

- **Domain exception hierarchy** — typed exceptions (`InsufficientStockException`, `HoldNotFoundException`, `HoldTerminatedException`) are mapped by `DomainExceptionHandler` to precise HTTP codes (409/404/410), avoiding any status-code logic in endpoint handlers.

- **MongoDB single-node replica set** — required for multi-document transactions (atomic stock decrement + hold insert). The healthcheck auto-initiates `rs.initiate()` on first boot.

- **`directConnection=true`** in MongoDB URI — prevents the .NET driver from following the replica set member hostname (which is `mongodb:27017` inside Docker) to `localhost:27017` on topology discovery.

- **Atomic write conflict retry** — `POST /api/holds` retries up to 3× (50ms backoff) on MongoDB write conflict (code 112 / "WriteConflict" in message). After 3 failures, returns 409 "Stock temporarily unavailable".

- **Hold expiry via background worker** — `HoldExpiryWorker` polls every 30s, uses `FindOneAndUpdate` with `{ status: Active, expiresAt ≤ now }` filter for atomic Active → Expired transition. Lost-race (DELETE won first) produces a null return — worker skips inventory restore and event publish.

- **Redis cache strategy** — `inventory:all` (30s TTL), `hold:{id}` (60s TTL), `settings:expiration-minutes` (60s TTL). All mutating operations (create/release/expire/reset) invalidate the relevant keys.

---

## Hold Lifecycle

```
                    ┌──────────┐
              POST  │  Active  │
             ──────►│          │
                    └────┬─────┘
              ┌──────────┼──────────┐
              │          │          │
         DELETE      Worker     expiresAt
              │      polls      elapsed
              ▼          │          │
        ┌──────────┐     ▼          ▼
        │ Released │   ┌──────────────┐
        └──────────┘   │   Expired   │
                       └──────────────┘
```

Both terminal states (Released, Expired) return 410 Gone on further DELETE attempts.

---

## Seed Data

Five products seeded on first startup:

| Product ID | Name | Total Qty |
|------------|------|-----------|
| widget-a | Widget A | 50 |
| widget-b | Widget B | 30 |
| gadget-x | Gadget X | 20 |
| device-z | Device Z | 10 |
| part-001 | Spare Part 001 | 100 |

---

## Configuration

All settings are in `appsettings.json` (Docker service names for container-to-container comms) and overridden for local dev in `appsettings.Development.json` (localhost).

| Key | Default | Notes |
|-----|---------|-------|
| `HoldSettings:ExpirationMinutes` | 15 | Hold TTL in minutes |
| `HoldSettings:PollingIntervalSeconds` | 30 | Background worker poll frequency |
| `MongoDb:DatabaseName` | inventory\_hold\_db | |

---

## Frontend

React 18 + TypeScript + Vite SPA served by nginx.

- **Server state**: TanStack Query v5 — auto-invalidates inventory and hold lists after every mutation
- **UI state**: Zustand — filter toggle, pagination, toast queue
- **Error handling**: 5xx/network errors → toast (auto-dismiss 4s); 4xx domain errors → inline `ErrorBanner`
- **Real-time countdown**: `HoldCard` counts down seconds remaining; on zero, invalidates the holds query

---

## AI Usage

See [AI/AI-USAGE.md](AI/AI-USAGE.md) for the full AI orchestration strategy, human audit, and verification approach.

---

## Demo Reset

After a demo session, restore the database to a clean state:

```bash
curl -X POST http://localhost:3000/api/inventory/reset
```
