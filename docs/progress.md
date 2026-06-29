# Implementation Progress — Inventory Hold Microservice

**Approach:** Vertical slice, riskiest first. TDD on every feature — write failing tests first, then minimum code to pass, then refactor. No big bang.

**References:** [hld.md](./hld.md) · [database-design.md](./database-design.md) · [design-discussion.md](../AI/design-discussion.md)

**Legend:** ⬜ Todo · 🔄 In Progress · ✅ Done

---

## Phase 0 — Infrastructure Skeleton

> Goal: `docker-compose up` starts all 4 services and they pass health checks. No app code yet. Validates the environment before writing a line of business logic.
>
> **Skills:** `docker-expert`

- ✅ **0.1** Create `docker-compose.yml` with 3 services: `mongodb`, `redis`, `rabbitmq` (`api` added in Phase 1)
- ✅ **0.2** Add health checks to each service
  - `mongodb` → `mongosh --eval "db.adminCommand('ping')" --quiet`
  - `redis` → `redis-cli ping`
  - `rabbitmq` → `rabbitmq-diagnostics ping` (start_period: 30s — broker takes ~20s)
- ✅ **0.3** `api` depends_on stub added as comment — wired in Phase 1 with `condition: service_healthy`
- ✅ **0.4** Named volume `mongo_data` declared
- ✅ **0.5** `.env` created (all connection defaults); `.gitignore` created
- ✅ **0.6** Verify: `docker-compose up -d` → all 3 show `healthy`; Compass connects; Redis pings; RabbitMQ UI at :15672

---

## Phase 1 — .NET Solution Scaffold

> Goal: Empty but buildable solution with correct DDD project structure, all NuGet packages installed, and xUnit test project wired up.
>
> **Skills:** `dotnet-architect` · `dotnet-backend` · `csharp-pro` · `ddd-strategic-design`

- ✅ **1.1** Solution `InventoryHold.sln` created; `src/` directory created
- ✅ **1.2** 5 projects created under `src/` (Contracts, Domain, Infrastructure, WebApi --no-openapi, UnitTests)
- ✅ **1.3** All project references wired per DDD dependency direction
- ✅ **1.4** NuGet packages installed: Infrastructure (MongoDB.Driver 3.9, StackExchange.Redis 3.0.7, RabbitMQ.Client 7.2.1); WebApi (Microsoft.AspNetCore.OpenApi 10.0.9, Scalar.AspNetCore 2.16.6, HealthChecks.MongoDb 9.0, HealthChecks.Redis 9.0); UnitTests (Moq 4.20, FluentAssertions 8.10)
- ✅ **1.5** `appsettings.json` (Docker service names) and `appsettings.Development.json` (localhost overrides) written
- ✅ **1.6** 4 config records in `Contracts/Settings/`: `MongoDbSettings`, `RedisSettings`, `RabbitMqSettings`, `HoldSettings`
- ✅ **1.7** `Program.cs` skeleton: config binding, OpenAPI + Scalar, ProblemDetails, middleware pipeline, TODO stubs per phase, temp `/health` stub
- ✅ **1.8** `dotnet build` → 0 errors · `dotnet test` → Passed: 1, Failed: 0

---

## Phase 2 — Domain Layer (TDD)

> Goal: Pure C# domain entities with full invariant enforcement. No infrastructure. Tests run in-memory with no mocks needed.
>
> **Skills:** `ddd-tactical-patterns` · `csharp-pro` · `clean-code`

### 2.1 — HoldItem Value Object
- ✅ **2.1.1** `[TEST]` Write `HoldItemTests`: quantity must be ≥ 1, productId must not be empty, productName must not be empty
- ✅ **2.1.2** Write `HoldItem` record in `Domain/Entities/HoldItem.cs` — make tests pass

### 2.2 — HoldStatus Enum
- ✅ **2.2.1** Write `HoldStatus` enum: `Active`, `Released`, `Expired` in `Domain/Entities/HoldStatus.cs`

### 2.3 — Hold Aggregate
- ✅ **2.3.1** `[TEST]` Write `HoldTests.Create`:
  - Hold created with correct `Id` (valid GUID), `status: Active`, `createdAt` set
  - `expiresAt = createdAt + expirationMinutes`
  - Empty items list throws `DomainException`
  - `customerName` can be null
- ✅ **2.3.2** `[TEST]` Write `HoldTests.StatusTransitions`:
  - `MarkReleased()` sets `status = Released`, `releasedAt` set, calling again throws
  - `MarkExpired()` sets `status = Expired`, `expiredAt` set, calling again throws
  - Cannot mark Expired hold as Released
- ✅ **2.3.3** Write `Hold` entity in `Domain/Entities/Hold.cs` — make all tests pass
- ✅ **2.3.4** Write domain exceptions: `DomainException`, `InsufficientStockException`, `HoldNotFoundException`, `HoldTerminatedException` in `Domain/Exceptions/`

### 2.4 — Repository & Service Interfaces
- ✅ **2.4.1** Write `IHoldRepository` in `Domain/Repositories/`:
  ```
  GetByIdAsync, GetPagedAsync, InsertAsync,
  AtomicTransitionAsync (findOneAndUpdate with status guard),
  GetExpiredActiveAsync
  ```
- ✅ **2.4.2** Write `IInventoryRepository` in `Domain/Repositories/`:
  ```
  GetAllAsync, GetByProductIdAsync, GetByProductIdsAsync ($in batch — B3),
  DecrementBatchAsync (within transaction),
  IncrementAsync, ResetAllAsync
  ```
- ✅ **2.4.3** Write `ISettingsRepository`: `GetExpirationMinutesAsync`
- ✅ **2.4.4** Write `IHoldEventPublisher` in `Domain/Messaging/`:
  ```
  PublishHoldCreatedAsync, PublishHoldReleasedAsync, PublishHoldExpiredAsync
  ```
- ✅ **2.4.5** Write `IInventoryCache` in `Domain/Cache/`:
  ```
  GetInventoryAsync, SetInventoryAsync, InvalidateInventoryAsync,
  GetHoldAsync, SetHoldAsync, InvalidateHoldAsync,
  GetExpirationMinutesAsync, SetExpirationMinutesAsync
  ```
- ✅ **2.4.6** Verify: `dotnet test` — 17 passed (HoldItemTests x8 + HoldTests x8 + PlaceholderTest x1), 0 infrastructure needed

---

## Phase 3 — MongoDB Infrastructure (TDD)

> Goal: MongoDB repositories and seeder implemented and verified with unit tests (mocked driver) + manual verification via MongoDB Compass.
>
> **Skills:** `mongodb-inventory-hold` · `dotnet-backend-patterns` · `error-handling-patterns`

### 3.1 — MongoDB Setup
- ✅ **3.1.1** Register `IMongoClient`, `IMongoDatabase`, and 3 typed collections in `Program.cs` via DI
- ✅ **3.1.2** Write `CollectionIndexInitializer` — declares all indexes on startup (idempotent):
  - `holds`: `{ status:1, expiresAt:1 }` and `{ status:1, createdAt:-1 }`
  - `inventory`: `{ productId:1 }` unique
- ✅ **3.1.3** Call index initializer and seeder from `Program.cs` startup pipeline

### 3.2 — Database Seeder
- ✅ **3.2.1** `[TEST]` Write `DatabaseSeederTests`:
  - Seeder inserts 5 products when inventory collection is empty
  - Seeder skips when inventory already has documents
- ✅ **3.2.2** Write `DatabaseSeeder` with seed data (widget-a:50, widget-b:30, gadget-x:20, device-z:10, part-001:100) — make tests pass

### 3.3 — MongoInventoryRepository
- ✅ **3.3.1** `[TEST]` Write `MongoInventoryRepositoryTests`:
  - `GetAllAsync` returns all products with computed `heldQuantity`
  - `GetByProductIdAsync` returns correct item; null when not found
  - `DecrementBatchAsync` applies `$inc -qty` for each item
  - `IncrementAsync` applies `$inc +qty` correctly
- ✅ **3.3.2** Write `MongoInventoryRepository` — implement all methods — make tests pass

### 3.4 — MongoHoldRepository
- ✅ **3.4.1** `[TEST]` Write `MongoHoldRepositoryTests`:
  - `GetByIdAsync` returns hold by GUID; null when not found
  - `GetPagedAsync` respects status filter, page, pageSize; returns correct total
  - `InsertAsync` inserts document and returns with `_id` set
  - `AtomicTransitionAsync` returns updated doc when status matches; null when guard fails
  - `GetExpiredActiveAsync` returns only Active holds past `expiresAt`
- ✅ **3.4.2** Write `MongoHoldRepository` — implement all methods — make tests pass

### 3.5 — MongoSettingsRepository
- ✅ **3.5.1** Write `MongoSettingsRepository.GetExpirationMinutesAsync` — reads `HoldExpirationMinutes` from settings collection; falls back to appsettings default
- ✅ **3.5.2** Verify with MongoDB Compass: indexes created, 5 seed products visible

---

## Phase 4 — POST /api/holds (TDD — Riskiest First)

> Goal: The most complex endpoint fully working — multi-document transaction, write conflict retry, all error cases. This is the load-bearing wall of the service.
>
> **Skills:** `mongodb-inventory-hold` · `api-endpoint-builder` · `error-handling-patterns` · `dotnet-backend`

### 4.1 — Hold Creation Service
- ✅ **4.1.1** `[TEST]` Write `CreateHoldServiceTests.HappyPath`:
  - Given all items in stock, creates hold, decrements inventory, returns hold
  - Hold has correct `expiresAt` (now + expirationMinutes)
  - Items array contains denormalized `productName` from inventory
- ✅ **4.1.2** `[TEST]` Write `CreateHoldServiceTests.Validation`:
  - `422` when `items` is empty
  - `422` when any `quantity <= 0`
  - `422` when `pageSize > 100` (list endpoint guard, add here for shared validation)
- ✅ **4.1.3** `[TEST]` Write `CreateHoldServiceTests.StockErrors`:
  - `404` when productId does not exist in inventory
  - `409` when `availableQty < requested` — response includes `failures[]` list with productId, requested, available per failing item
  - All-or-nothing: if one item fails, no inventory is mutated
- ✅ **4.1.4** `[TEST]` Write `CreateHoldServiceTests.WriteConflict`:
  - On `MongoCommandException` code 112: retries up to 3× with 50ms backoff
  - After 3 failures: returns `409` with "Stock temporarily unavailable" message
  - Does NOT retry on other exception types
- ✅ **4.1.5** Write `HoldService.CreateHoldAsync` — make all tests pass
  - Transaction: Phase 1 validate all → Phase 2 decrement + insert
  - Wrap in write conflict retry loop (catch code 112 only)
- ✅ **4.1.6** Register `HoldService` in DI

### 4.2 — POST Endpoint
- ✅ **4.2.1** Write `CreateHoldRequest` record in `Contracts/Requests/` with `customerName?`, `items[]`
- ✅ **4.2.2** Write `HoldResponse` record in `Contracts/Responses/` with all hold fields
- ✅ **4.2.3** Write `POST /api/holds` minimal API endpoint in `WebApi/Endpoints/HoldEndpoints.cs`
  - Map `201 Created` with `Location: /api/holds/{holdId}`
  - Map service exceptions to correct HTTP codes via exception middleware
- ✅ **4.2.4** Write `ExceptionMiddleware` mapping domain exceptions → RFC 7807 `ProblemDetails`:
  - `DomainException` → 422
  - `InsufficientStockException` → 409 with `failures` extension
  - `HoldNotFoundException` → 404
  - `HoldTerminatedException` → 410
  - Write conflict exhausted → 409
  - Unhandled → 500 (sanitized message)
- ✅ **4.2.5** Manual test: `docker-compose up` → POST to `/api/holds` → `201 Created` with holdId

---

## Phase 5 — Background Worker (TDD)

> Goal: HoldExpiryWorker correctly expires holds atomically, restores inventory, and handles the race condition with DELETE.
>
> **Skills:** `dotnet-backend` · `mongodb-inventory-hold` · `error-handling-patterns`

- ✅ **5.1** `[TEST]` Write `HoldExpiryWorkerTests.NoExpiredHolds`:
  - When no holds are past `expiresAt`: 0 MongoDB writes, 0 cache operations
- ✅ **5.2** `[TEST]` Write `HoldExpiryWorkerTests.ExpiresHolds`:
  - Given 2 expired holds: transitions both to Expired, increments inventory for each item
- ✅ **5.3** `[TEST]` Write `HoldExpiryWorkerTests.RaceCondition`:
  - `AtomicTransitionAsync` returns null (lost race): skip inventory restore, skip event publish
- ✅ **5.4** `[TEST]` Write `HoldExpiryWorkerTests.CacheInvalidation`:
  - When ≥ 1 hold expired: `InvalidateInventoryAsync` called exactly once
  - When 0 holds expired: `InvalidateInventoryAsync` never called
- ✅ **5.5** Write `HoldExpiryWorker : BackgroundService` in `WebApi/Workers/`
  - Poll every `HoldSettings:PollingIntervalSeconds` (default 30s)
  - Use `CancellationToken` for graceful shutdown
  - Wrap each iteration in try/catch — log errors, never crash the worker
- ✅ **5.6** Register `HoldExpiryWorker` as hosted service in `Program.cs`
- ✅ **5.7** Verify: create a hold → wait 15 min (or lower `HoldExpirationMinutes` to 1 for testing) → hold status becomes `Expired`, inventory restored

---

## Phase 6 — GET & DELETE Endpoints (TDD)

> Goal: All remaining hold CRUD endpoints working with correct HTTP codes for all cases.
>
> **Skills:** `api-endpoint-builder` · `mongodb-inventory-hold` · `dotnet-backend`

### 6.1 — GET /api/holds/{holdId}
- ✅ **6.1.1** `[TEST]` Write `GetHoldTests`:
  - Returns `200 OK` with hold when found (Active, Released, or Expired)
  - Returns `404` when holdId never existed
  - Cache: returns from cache on second call (verify `IInventoryCache.GetHoldAsync` called first)
- ✅ **6.1.2** Write `GET /api/holds/{holdId}` endpoint — cache check → MongoDB fallback → set cache

### 6.2 — DELETE /api/holds/{holdId}
- ✅ **6.2.1** `[TEST]` Write `ReleaseHoldTests`:
  - `200 OK` with released hold (including `releasedAt`) on Active hold
  - `404` when holdId does not exist at all
  - `410 Gone` when hold exists with status `Released` — detail includes `releasedAt`
  - `410 Gone` when hold exists with status `Expired` — detail includes `expiredAt`
  - On success: `IInventoryRepository.IncrementAsync` called for each item
  - On success: `IInventoryCache.InvalidateInventoryAsync` called
  - On success: `IInventoryCache.InvalidateHoldAsync` called
- ✅ **6.2.2** Write `HoldService.ReleaseHoldAsync` — make tests pass
  - `AtomicTransitionAsync` → if null: fetch hold for 404 vs 410 distinction
- ✅ **6.2.3** Write `DELETE /api/holds/{holdId}` endpoint

### 6.3 — GET /api/holds (List)
- ✅ **6.3.1** `[TEST]` Write `ListHoldsTests`:
  - Default: returns page 1, size 20, status Active
  - `?status=expired` filters correctly
  - Pagination: correct `total`, `totalPages`, `page`, `pageSize` in response
  - `422` when `pageSize > 100`
  - `422` when `pageSize < 1`
- ✅ **6.3.2** Write `GET /api/holds` endpoint with query params `status`, `page`, `pageSize`
- ✅ **6.3.3** Write `PagedResponse<T>` in `Contracts/Responses/`
- ✅ **6.3.4** Verify all 3 endpoints manually with Scalar UI
- ✅ **6.3.5** `GET /api/holds/cursor` — keyset pagination endpoint (B5): `CursorPagedResponse<T>`, cursor=`"{createdAt:O}|{id}"`, O(log n)

---

## Phase 7 — GET /api/inventory + Reset (TDD)

> Goal: Inventory read endpoint with full `totalQty / availableQty / heldQty` breakdown. Reset endpoint for demos.
>
> **Skills:** `api-endpoint-builder` · `mongodb-inventory-hold`

- ✅ **7.1** `[TEST]` Write `GetInventoryTests`:
  - Returns all 5 products with correct `heldQuantity = totalQty - availableQty`
  - Returns from Redis cache on second call
  - Cache populated after first DB read
- ✅ **7.2** Write `GET /api/inventory` endpoint — cache check → DB fallback → set cache → return
- ✅ **7.3** Write `InventoryItemResponse` record in `Contracts/Responses/`
- ✅ **7.4** `[TEST]` Write `ResetInventoryTests`:
  - Deletes all holds
  - Restores `availableQty = totalQty` for all inventory items
  - Calls `IInventoryCache.FlushAllAsync`
- ✅ **7.5** Write `POST /api/inventory/reset` endpoint
- ✅ **7.6** Verify: POST hold → check inventory → reset → inventory back to seed quantities

---

## Phase 8 — RabbitMQ Publisher (TDD)

> Goal: All 3 hold lifecycle events published to the correct exchange and queues. Fire-and-forget error handling in place.
>
> **Skills:** `rabbitmq-inventory-hold` · `event-sourcing-architect` · `dotnet-backend-patterns`

- ✅ **8.1** `[TEST]` Write `RabbitMqPublisherTests` (with mocked `IConnection`/`IChannel`):
  - `PublishHoldCreatedAsync` serializes correct payload fields (holdId, customerName, status, items[], createdAt, expiresAt)
  - `PublishHoldReleasedAsync` serializes `releasedAt`, not `expiredAt`
  - `PublishHoldExpiredAsync` serializes `expiredAt`, not `releasedAt`
  - Uses correct routing keys: `hold.created`, `hold.released`, `hold.expired`
  - Fire-and-forget: channel throws → logged at Error, no exception propagated
- ✅ **8.2** Write event DTO records: `HoldCreatedEvent`, `HoldReleasedEvent`, `HoldExpiredEvent`, `EventItem` in `Contracts/Events/`
- ✅ **8.3** Write `RabbitMqConnectionFactory` (static helper, auto-recovery enabled)
- ✅ **8.4** Write `RabbitMqTopologyInitializer` — idempotent exchange + 3 queue declarations with bindings
- ✅ **8.5** Write `RabbitMqHoldEventPublisher` — one channel per publish, fire-and-forget error handling
- ✅ **8.6** Register `IConnection` (singleton), `RabbitMqHoldEventPublisher` in `Program.cs`
- ✅ **8.7** Call `RabbitMqTopologyInitializer.InitializeAsync` at startup
- ✅ **8.8** Wire publisher into:
  - `HoldService.CreateHoldAsync` → `PublishHoldCreatedAsync` (after commit, fire-and-forget)
  - `HoldService.ReleaseHoldAsync` → `PublishHoldReleasedAsync` (after cache invalidation, fire-and-forget)
  - `HoldExpiryWorker` already had `PublishHoldExpiredAsync` — no change needed
- ✅ **8.9** `[TEST]` `FireAndForget` test included in `RabbitMqPublisherTests` + wiring tests in `CreateHoldServiceTests` and `ReleaseHoldServiceTests`
- ✅ **8.10** Verify: create hold → RabbitMQ Management UI (`:15672`) → `hold.created.queue` has 1 message with correct JSON

---

## Phase 9 — Redis Caching (TDD)

> Goal: Cache layer fully wired. Inventory reads hit Redis. Hold reads hit Redis. Settings cached. Invalidation correct on every mutation.
>
> **Skills:** `dotnet-backend-patterns` · `azure-resource-manager-redis-dotnet` · `application-performance-performance-optimization`

- ✅ **9.1** `[TEST]` Write `RedisCacheServiceTests` (mock `IConnectionMultiplexer`):
  - `GetInventoryAsync` returns deserialized list on hit; null on miss
  - `SetInventoryAsync` serializes and sets with 30s TTL
  - `InvalidateInventoryAsync` deletes `inventory:all` key
  - `GetHoldAsync` returns deserialized hold on hit; null on miss
  - `SetHoldAsync` serializes and sets with 60s TTL for key `hold:{holdId}`
  - `InvalidateHoldAsync` deletes `hold:{holdId}`
  - `GetExpirationMinutesAsync` returns parsed int on hit; null on miss
  - `SetExpirationMinutesAsync` sets with 60s TTL
- ✅ **9.2** Write `RedisCacheService` implementing `IInventoryCache` — make tests pass
  - Use `IDatabase` from `StackExchange.Redis`
  - Serialize/deserialize with `System.Text.Json`
- ✅ **9.3** Register `IConnectionMultiplexer` (singleton) and `RedisCacheService` in `Program.cs`
- ✅ **9.4** Wire cache into `GET /api/inventory`:
  - Try cache → miss → hit DB → set cache → return
- ✅ **9.5** Wire cache into `GET /api/holds/{holdId}`:
  - Try cache → miss → hit DB → set cache → return
- ✅ **9.6** Wire cache invalidation:
  - `POST /api/holds` success → DEL `inventory:all`
  - `DELETE /api/holds/{id}` success → DEL `inventory:all`, DEL `hold:{id}`
  - `HoldExpiryWorker` (if any expired) → DEL `inventory:all`
  - `POST /api/inventory/reset` → flush all
- ✅ **9.7** Wire settings cache into `CreateHoldAsync`:
  - Try Redis → miss → hit MongoDB → set Redis → return
- ✅ **9.8** Verify: `GET /api/inventory` twice → check Redis CLI `GET inventory:all` is populated after first call

---

## Phase 10 — Health Checks + Swagger (TDD-light)

> Goal: `/health` reports per-dependency status. Swagger UI works at `/swagger`. docker-compose startup ordering validated.
>
> **Skills:** `dotnet-backend` · `api-documentation-generator`

- ✅ **10.1** Add health check packages to `WebApi`:
  - `AspNetCore.HealthChecks.MongoDb` + `AspNetCore.HealthChecks.Redis` (already in Phase 1)
  - Write `RabbitMqHealthCheck` (checks `IConnection.IsOpen`)
- ✅ **10.2** Register all 3 health checks in `Program.cs` with tags (`mongodb`, `redis`, `rabbitmq`)
- ✅ **10.3** Map `GET /health` endpoint — `{"status":"Healthy"}` when all pass; adds `checks` detail when any unhealthy
- ✅ **10.4** `[TEST]` Write `RabbitMqHealthCheckTests` (3 tests): IsOpen→Healthy, IsClosed→Unhealthy, Exception→Unhealthy
- ✅ **10.5** `.NET 10 native OpenAPI` + Scalar already wired from Phase 1 (`/scalar/v1` works)
- ✅ **10.6** All endpoints annotated with `WithName`, `WithSummary`, `Produces<T>`, `ProducesProblem`
- ✅ **10.7** Verify: `docker-compose up` → `GET /health` → `{"status":"Healthy"}`, `/scalar/v1` → UI loads

---

## Phase 11 — Unit Test Suite Completion

> Goal: Minimum 5 passing tests, all infrastructure mocked, covering spec-required scenarios.
>
> **Skills:** `csharp-pro` · `clean-code` · `dotnet-backend-patterns`

- ✅ **11.1** `dotnet test` → **Passed: 88, Failed: 0** (all phases 2–10 + B1–B12 perf fixes)
- ✅ **11.2** Mandatory 5 scenarios covered by named tests:
  - ✓ `CreateHoldAsync_EmptyItems_ThrowsDomainException` → validation 422
  - ✓ `CreateHoldAsync_AllInStock_ReturnsHoldWithDenormalizedProductName` → happy path 201
  - ✓ `CreateHoldAsync_InsufficientStock_ThrowsWithAllFailures` → 409 with failures[]
  - ✓ `CreateHoldAsync_WriteConflict_RetriesThreeTimesAndThrows` → retry 3× then 409
  - ✓ `ProcessExpiredHoldsAsync_RaceCondition_...` → AtomicTransition null → skip restore
- ✅ **11.3** 0 tests require Docker — all dependencies mocked via Moq
- ⬜ **11.4** Optional: coverage report

---

## Phase 15 — Performance Bottleneck Fixes (B1–B12)

> Goal: Address all 12 bottlenecks from the senior architect review. No regressions — 88/88 tests pass.

- ✅ **B1** RabbitMQ channel reuse — `SemaphoreSlim(1,1)` guard + `IAsyncDisposable`; creates channel once, reuses until closed
- ✅ **B2** `BulkWriteAsync` (`IsOrdered=false`) for `DecrementBatchAsync` and `IncrementAsync` — N `UpdateOneAsync` → 1 batch write
- ✅ **B3** `GetByProductIdsAsync` with `Filter.In` — N sequential `GetByProductIdAsync` calls → 1 `$in` query in `CreateHoldAsync`
- ✅ **B4** Cache stampede guard in `InventoryService` — `static SemaphoreSlim _fetchLock` + double-check post-lock re-read
- ✅ **B5** `GET /api/holds/cursor` — keyset pagination; cursor=`"{createdAt:O}|{id}"`; `Limit=pageSize+1` trick; O(log n) vs O(n) skip
- ✅ **B6** `EstimatedDocumentCountAsync` for total count when no filter — O(1) metadata read vs O(n) full scan
- ✅ **B7** `HoldExpiryWorker` safety — `_tickLock.WaitAsync(0)` overlap guard; `Parallel.ForEachAsync(DOP=8)`; `Interlocked.Increment` counter; `Limit=500` on `GetExpiredActiveAsync`
- ✅ **B8** Fixed-window rate limiter on `POST /api/holds` — 100 req/min via `AddRateLimiter` + `RequireRateLimiting("holds-create")`
- ✅ **B9** Max 50 items guard in `CreateHoldAsync` — `422` if `request.Items.Count > 50`
- ✅ **B10** `BulkWriteAsync` for `ResetAllAsync` — 1 `GetAllAsync` + 1 `BulkWrite` instead of N `UpdateOneAsync`
- ✅ **B11** Batch `KeyDeleteAsync(RedisKey[])` in `FlushAllAsync` — 2 sequential DELs → 1 batch DEL
- ✅ **B12** `Task.Run()` wrap on RabbitMQ `CreateAsync` in DI factory — strips sync context, avoids threadpool deadlock risk at startup

---

## Phase 12 — Frontend (TDD-light)

> Goal: React SPA with all 4 required views working against the real API.
>
> **Skills:** `frontend-developer` · `api-endpoint-builder` · `e2e-testing`

### 12.1 — Scaffold
- ✅ **12.1.1** `npm create vite@latest frontend -- --template react-ts`
- ✅ **12.1.2** Install dependencies: `@tanstack/react-query` v5, `zustand`, `axios`; dev deps: `vitest`, `@testing-library/react`, `@testing-library/user-event`, `@testing-library/jest-dom`, `happy-dom`
- ✅ **12.1.3** Set up `QueryClient` provider in `App.tsx` (wraps all 3 section components)
- ✅ **12.1.4** Set up Zustand store: `useStore` with `activeOnlyFilter`, `currentPage`, `toasts` queue
- ✅ **12.1.5** Write API client functions in `src/api/` (typed with TS interfaces):
  - `getInventory()`, `resetInventory()`, `getHolds(status?, page, pageSize)`, `getHold(id)`, `createHold(body)`, `releaseHold(id)`

### 12.2 — TypeScript Types
- ✅ **12.2.1** Write `src/types/api.ts`:
  - `InventoryItem`, `Hold`, `HoldItem`, `HoldStatus`, `PagedResponse<T>`, `CreateHoldRequest`, `ProblemDetails`, `StockFailure`

### 12.3 — Inventory Dashboard
- ✅ **12.3.1** `[TEST]` Write component test: renders product name + quantities, `heldQuantity > 0` row gets `held` CSS class
- ✅ **12.3.2** Write `InventoryDashboard` component:
  - TanStack Query v5 `useQuery({ queryKey: ['inventory'], queryFn: getInventory })`
  - Table: product name, totalQuantity, availableQuantity, heldQuantity (row highlighted when heldQuantity > 0)
  - Skeleton loader while `isLoading`; Reset Inventory button with spinner

### 12.4 — Create Hold Form
- ✅ **12.4.1** `[TEST]` Write component test: form submits correct payload, shows ErrorBanner on 409 with `detail` text
- ✅ **12.4.2** Write `CreateHoldForm` component:
  - Optional `customerName` text input
  - Product selector dropdown (from `getInventory`) — already-selected products removed from options
  - Quantity input per item; Add item / Remove item buttons
  - `useMutation({ mutationFn: createHold, onSuccess: invalidate(['holds','inventory']) })`
  - `onError`: sets `error` state from `err.response?.data?.detail` → `<ErrorBanner>`
  - Spinner on submit button while `isPending`; form resets on success

### 12.5 — Active Holds List
- ✅ **12.5.1** `[TEST]` Write component test: renders hold customerName, filter toggle calls `getHolds(undefined,...)`, next page increments currentPage
- ✅ **12.5.2** Write `HoldsList` component:
  - Zustand `activeOnlyFilter` toggle (ON = `?status=active`, OFF = omit param → all statuses)
  - TanStack Query v5 `useQuery({ queryKey: ['holds', { status, page }], queryFn: () => getHolds(status, page) })`
  - Pagination controls: Prev/Next, disabled at boundaries, "Page X of Y"

### 12.6 — Release Hold + HoldCard
- ✅ **12.6.1** `[TEST]` Write component tests: countdown shows, Release button Active-only, inline confirm appears, confirm calls `releaseHold`
- ✅ **12.6.2** Write `HoldCard` component:
  - Countdown: `secondsLeft` local state via `setInterval`; on 0 → `invalidateQueries(['holds'])`
  - Status badge: Active=green, Released=grey, Expired=red
  - Release button (Active only): `confirming` boolean for inline "Confirm? / Cancel" pattern
  - `useMutation(releaseHold)` with `onSuccess: invalidate(['holds','inventory'])`; on 410: show `errorMessage`

### 12.7 — Error Handling
- ✅ **12.7.1** Write `ErrorBanner` component for inline domain errors (renders nothing when `message` is null)
- ✅ **12.7.2** Write `Toast` component — subscribes to Zustand `toasts` queue; each toast auto-dismisses after 4s via `useEffect`
- ✅ **12.7.3** Axios interceptor: 5xx (or network error) → `useStore.getState().addToast(...)` (works outside React); 4xx re-thrown to component
- ✅ **12.7.4** Write `LoadingSkeleton` (shimmer rows) and `LoadingSpinner` (button spinner)

---

## Phase 13 — Nginx + Docker Full Integration

> Goal: `docker-compose up --build` starts everything including frontend. Single command, everything works.
>
> **Skills:** `docker-expert` · `microservices-patterns`

- ✅ **13.1** Write `frontend/Dockerfile` — multi-stage:
  - Stage 1 `build`: `node:20-alpine` → `npm ci` → `npm run build`
  - Stage 2 `final`: `nginx:alpine` → copy `/dist` → copy nginx config
- ✅ **13.2** Write `frontend/nginx.conf`:
  - Serve `index.html` for all non-API routes (SPA fallback)
  - `location /api/` → `proxy_pass http://api:8080/api/`
  - `location /health` → `proxy_pass http://api:8080/health`
- ✅ **13.3** Add `frontend` service to `docker-compose.yml`:
  - Build from `./frontend/Dockerfile`; expose port `80`; `depends_on: api: condition: service_healthy`
- ✅ **13.4** Write `api/Dockerfile` — multi-stage:
  - Stage 1 `build`: `mcr.microsoft.com/dotnet/sdk:10.0` → `dotnet publish`
  - Stage 2 `final`: `mcr.microsoft.com/dotnet/aspnet:10.0` → non-root `appuser`; `ASPNETCORE_URLS=http://+:8080`
- ✅ **13.5** Add `api` service to `docker-compose.yml`:
  - `build.context: .`, `dockerfile: api/Dockerfile`; `depends_on` mongodb+redis+rabbitmq (all service_healthy); healthcheck via `curl -sf http://localhost:8080/health`
- ✅ **13.6** Run full integration: `docker-compose up --build`
  - All 5 services start healthy
  - `http://localhost:3000` → React app loads (port 80 restricted on Windows; mapped to 3000)
  - `GET http://localhost:3000/health` → `{"status":"Healthy"}` (proxied through nginx)
  - `GET http://localhost:3000/api/inventory` → 5 products with correct quantities

---

## Phase 14 — Final QA + README

> Goal: All scenarios from HLD verified. README complete. Submission ready.
>
> **Skills:** `e2e-testing` · `api-documentation-generator` · `architecture-decision-records`

### QA Checklist (from HLD scenarios)
- ✅ **14.1** Scenario: Create hold happy path → 201 with correct payload
- ✅ **14.2** Scenario: Insufficient stock → 409 with `failures[]` listing device-z
- ✅ **14.3** Scenario: Concurrent requests → second request retries, eventually 409
- ✅ **14.4** Scenario: GET hold by ID → 200 for all states, 404 for unknown
- ✅ **14.5** Scenario: DELETE active hold → 200 with `releasedAt`, inventory restored
- ✅ **14.6** Scenario: DELETE expired hold → 410 with expiry detail
- ✅ **14.7** Scenario: DELETE non-existent hold → 404
- ✅ **14.8** Scenario: Background worker expiry → `HoldExpirationMinutes: 1`, hold → Expired within 30s, inventory restored
- ✅ **14.9** Scenario: GET inventory cache → Redis `inventory:all` key populated after first call
- ✅ **14.10** Scenario: RabbitMQ events → all 3 queues receive messages (verified via Management UI)
- ✅ **14.11** Scenario: Reset endpoint → all holds cleared, inventory back to seed quantities
- ✅ **14.12** `dotnet test` → 88 passed, 0 failed
- ✅ **14.13** `docker-compose up --build` from cold → all 5 services healthy within 60s

### README
- ✅ **14.14** `README.md` complete: prerequisites, one-command startup, service URLs, test instructions, reset demo, HLD Mermaid diagram, seed data, configuration table, architecture section, frontend section

---

## Phase 16 — Post-Review Corrections

> Bugs and correctness issues identified during senior interviewer self-review, fixed after submission.

- ✅ **16.1** **Cursor compound predicate** — `GetPagedByCursorAsync` previously filtered only on `Lt(createdAt)`, silently skipping holds with identical millisecond timestamps at page boundaries. Fixed to `Or(Lt(createdAt), And(Eq(createdAt), Lt(id)))` with compound sort `(createdAt DESC, id DESC)`. `MongoHoldRepository.cs`
- ✅ **16.2** **Repositories Singleton → Scoped** — `IHoldRepository`, `IInventoryRepository`, `ISettingsRepository`, `ITransactionFactory` changed from `AddSingleton` to `AddScoped` in `Program.cs`. MongoDB state is per-request; Singleton lifetime was a latent bug if any per-request state was ever added.
- ✅ **16.3** **`HoldExpiryWorker` → `IServiceScopeFactory`** — `BackgroundService` is Singleton and cannot take Scoped dependencies directly. Refactored to inject `IServiceScopeFactory`; scope created inside the tick lock per execution (not per tick attempt — skipped ticks never allocate a scope). Updated `HoldExpiryWorkerTests` with full `ScopeFactory → IServiceScope → IServiceProvider` mock chain.
- ✅ **16.4** **`HoldCard` countdown drift** — `setInterval(() => setSecondsLeft(s => s - 1), 1000)` accumulates drift when the browser tab is backgrounded (throttled timers). After 3 minutes the displayed time diverges ~25s from reality, visible as a jump on page refresh. Fixed: each tick now recalculates `Math.floor((new Date(expiresAt) - Date.now()) / 1000)` from wall clock. Effect dependency changed from `[secondsLeft, ...]` to `[expiresAt, ...]` — interval registered once per hold, not re-registered every second.
- ✅ **16.5** **README HLD diagram** — Mermaid `flowchart TD` added to `README.md` showing full path: Browser → nginx → API layers (Endpoints → Services → Domain → Infrastructure) → MongoDB/Redis/RabbitMQ, plus `HoldExpiryWorker` polling loop.
- ✅ **16.6** **AI-USAGE Human Audit Part 2** — replaced implementation-phase corrections list with architectural trade-off analysis: Background Worker vs In-Process Timer/Delegate vs RabbitMQ DLX, with pros/cons table at millions-of-customers scale and justification for the polling choice.

---

## Skill Map Quick Reference

| Phase | Skills Used |
|-------|------------|
| 0 — Docker skeleton | `docker-expert` |
| 1 — Solution scaffold | `dotnet-architect` · `dotnet-backend` · `csharp-pro` · `ddd-strategic-design` |
| 2 — Domain layer | `ddd-tactical-patterns` · `csharp-pro` · `clean-code` |
| 3 — MongoDB infra | `mongodb-inventory-hold` · `dotnet-backend-patterns` · `error-handling-patterns` |
| 4 — POST /api/holds | `mongodb-inventory-hold` · `api-endpoint-builder` · `error-handling-patterns` · `dotnet-backend` |
| 5 — Background worker | `dotnet-backend` · `mongodb-inventory-hold` · `error-handling-patterns` |
| 6 — GET/DELETE endpoints | `api-endpoint-builder` · `mongodb-inventory-hold` · `dotnet-backend` |
| 7 — Inventory + Reset | `api-endpoint-builder` · `mongodb-inventory-hold` |
| 8 — RabbitMQ | `rabbitmq-inventory-hold` · `event-sourcing-architect` · `dotnet-backend-patterns` |
| 9 — Redis | `dotnet-backend-patterns` · `azure-resource-manager-redis-dotnet` · `application-performance-performance-optimization` |
| 10 — Health + Swagger | `dotnet-backend` · `api-documentation-generator` |
| 11 — Test suite | `csharp-pro` · `clean-code` · `dotnet-backend-patterns` |
| 12 — Frontend | `frontend-developer` · `api-endpoint-builder` · `e2e-testing` |
| 13 — Nginx + Docker | `docker-expert` · `microservices-patterns` |
| 14 — QA + README | `e2e-testing` · `api-documentation-generator` · `architecture-decision-records` |

---

## Progress Summary

| Phase | Description | Status |
|-------|-------------|--------|
| 0 | Infrastructure skeleton | ✅ |
| 1 | .NET solution scaffold | ✅ |
| 2 | Domain layer (TDD) | ✅ |
| 3 | MongoDB infrastructure (TDD) | ✅ |
| 4 | POST /api/holds (TDD) | ✅ |
| 5 | Background worker (TDD) | ✅ |
| 6 | GET + DELETE endpoints (TDD) | ✅ |
| 7 | Inventory + Reset (TDD) | ✅ |
| 8 | RabbitMQ publisher (TDD) | ✅ |
| 9 | Redis caching (TDD) | ✅ |
| 10 | Health checks + Swagger | ✅ |
| 11 | Unit test suite (88 tests) | ✅ |
| 12 | Frontend React | ✅ |
| 13 | Nginx + full Docker | ✅ |
| 14 | Final QA + README | ✅ |
| 15 | Performance fixes (B1–B12) | ✅ |
| 16 | Post-review corrections | ✅ |
