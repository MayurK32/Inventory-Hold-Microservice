# AI-USAGE.md
## Inventory Hold Microservice — Kibo Commerce Assignment

---

## AI Strategy
*(To be documented during and after development)*

**Tools Used:** Claude Code (claude-sonnet-4-6), Claude Code Max effort mode

**Context Management Approach:**
- Provided full assignment PDF as source of truth
- Conducted structured Q&A to resolve all ambiguities before writing any code
- Used AI-PROMPT-LOG.md to track all significant prompt decisions
- All architectural decisions documented before implementation
- Before writing any code, collected all skills required for implementation from the `antigravity-awesome-skills` skill library and installed them under `.claude/skills/` — 27 skills covering .NET/C#, DDD, Docker, API design, event sourcing, Redis/MongoDB, clean code, error handling, performance, E2E testing, and React frontend

---

## Design Discussion

**Brainstorm Duration:** 1 hour 30 minutes

Before writing a single line of code, a structured Q&A session was conducted to surface and resolve every ambiguity in the assignment spec. 23 questions were asked and answered, covering concurrency model, expiry mechanism, API contracts, caching strategy, RabbitMQ topology, frontend architecture, and edge cases.

Full Q&A session and decisions summary: **[design-discussion.md](./design-discussion.md)**

The file contains:
- 23 questions with the full trade-off analysis presented for each
- The decision made and the reasoning behind it
- Architectural implications of each decision
- A final decisions summary table for quick reference

---

## Database Design

Full database design including ER diagram, collection schemas, index definitions, state machine, quantity flow, and cross-collection operation patterns: **[database-design.md](../docs/database-design.md)**

## Implementation Progress

Step-by-step TDD implementation plan across 15 phases (Phase 0–14), each task mapped to the relevant skill: **[progress.md](../docs/progress.md)**

## High-Level Design

Full HLD covering system architecture, component diagram, all API endpoints, and detailed sequence/flow diagrams for every scenario — create hold, insufficient stock, concurrent write conflict, release, background worker expiry, worker vs client race condition, inventory cache, and RabbitMQ event flow: **[hld.md](../docs/hld.md)**

---

## Phase 0 — Infrastructure Skeleton

**Goal:** Get all 3 infrastructure services (MongoDB, Redis, RabbitMQ) running and healthy in Docker before writing any .NET code. Validate the environment first.

**Planning approach:**
- Used Claude Code plan mode to design the docker-compose structure
- Questioned image version choices — AI initially proposed unpinned tags (`mongo:7`, `redis:7-alpine`, `rabbitmq:3-management`)
- Human caught the version pinning issue; AI corrected to minor-version pins (`mongo:7.0`, `redis:7.4-alpine`, `rabbitmq:3.13-management`) with explicit compatibility verification against .NET 10 NuGet packages
- Human caught `.env` commit mistake — AI had planned to commit it with dev defaults. Corrected: `.env` excluded via `.gitignore`, `.env.example` committed instead

**Files created:**
- `docker-compose.yml` — 3 services with health checks, named volume `mongo_data`, `api` service deferred to Phase 1
- `.env.example` — all required environment variables as a setup template
- `.gitignore` — .NET + Node + Docker ignore rules; `.env` explicitly excluded

**Human decisions that overrode AI defaults:**
1. Version pinning — AI used floating minor tags; human required minor-pinned tags
2. `.env` not committed — AI planned to commit it; human rejected; switched to `.env.example` pattern

---

## Phase 1 — .NET Solution Scaffold

**Goal:** Create an empty-but-buildable .NET 10 solution with the 5-project DDD structure, all NuGet packages, config records, and a minimal Program.cs skeleton. No infrastructure connectivity yet.

**Planning approach:**
- Used Claude Code plan mode; AI read `docs/hld.md §3`, `docs/progress.md`, and the assignment PDF via Explore agent to derive the exact project structure and package list
- AI corrected NuGet placement from original progress.md: drivers (MongoDB.Driver, StackExchange.Redis, RabbitMQ.Client) live in **Infrastructure only**, not in WebApi — WebApi gets them transitively
- AI chose `Scalar.AspNetCore` over `Swashbuckle` (Swashbuckle is unmaintained for .NET 9+)
- AI flagged Redis connection string format gotcha: StackExchange.Redis uses `host:port`, NOT `redis://host:port` — documented in RedisSettings.cs

**Files created:**
- `InventoryHold.sln` + `src/` with 5 projects wired per DDD dependency direction
- `src/InventoryHold.Contracts/Settings/` — 4 config records: `MongoDbSettings`, `RedisSettings`, `RabbitMqSettings`, `HoldSettings`
- `src/InventoryHold.WebApi/appsettings.json` (Docker service names) + `appsettings.Development.json` (localhost overrides)
- `src/InventoryHold.WebApi/Program.cs` — config binding, OpenAPI/Scalar, ProblemDetails, middleware pipeline, TODO stubs per phase

**Actual NuGet versions installed:**
| Package | Version |
|---------|---------|
| MongoDB.Driver | 3.9.0 |
| StackExchange.Redis | 3.0.7 |
| RabbitMQ.Client | 7.2.1 |
| Microsoft.AspNetCore.OpenApi | 10.0.9 |
| Scalar.AspNetCore | 2.16.6 |
| AspNetCore.HealthChecks.MongoDb | 9.0.0 |
| AspNetCore.HealthChecks.Redis | 9.0.0 |
| Moq | 4.20.72 |
| FluentAssertions | 8.10.0 |

**Verification:** `dotnet build` → 0 errors · `dotnet test` → Passed: 1, Failed: 0 · `/health` → 200 · `/scalar/v1` → UI loads

---

## Phase 2 — Domain Layer (TDD)

**Goal:** Pure C# domain model with zero infrastructure dependencies. Establish all invariants and interface contracts that every later phase depends on.

**Planning approach:**
- Used Claude Code plan mode; AI read progress.md, database-design.md, ddd-tactical-patterns skill, and mongodb-inventory-hold skill to derive exact entity shapes and interface signatures
- Key design decision: defined `IMongoTransaction` + `ITransactionFactory` as pure Domain abstractions so repository interfaces don't reference `IClientSessionHandle` (MongoDB.Driver) — preserves strict layer boundaries

**TDD flow:**
1. Wrote test files first (red — compile error until implementation added)
2. Wrote exceptions → entities → interfaces (green)
3. `dotnet test` → 17/17 passed with no mocks, no Docker, no infrastructure

**Files created (18 total):**

| Layer | Files |
|-------|-------|
| UnitTests/Domain/ | `HoldItemTests.cs` (8 tests), `HoldTests.cs` (8 tests) |
| Domain/Entities/ | `HoldStatus.cs`, `HoldItem.cs`, `Hold.cs`, `InventoryItem.cs`, `AppSetting.cs` |
| Domain/Exceptions/ | `DomainException.cs`, `InsufficientStockException.cs`, `HoldNotFoundException.cs`, `HoldTerminatedException.cs` |
| Domain/Transactions/ | `IMongoTransaction.cs`, `ITransactionFactory.cs` |
| Domain/Repositories/ | `IHoldRepository.cs`, `IInventoryRepository.cs`, `ISettingsRepository.cs` |
| Domain/Messaging/ | `IHoldEventPublisher.cs` |
| Domain/Cache/ | `IInventoryCache.cs` |

**Key invariants enforced:**
- `HoldItem`: productId non-empty, productName non-empty, quantity ≥ 1 (throws `DomainException` on violation)
- `Hold.Create`: items list non-empty; Id is a valid GUID; expiresAt = createdAt + expirationMinutes
- Status transitions: Active → Released OR Active → Expired (one-way); double transition throws `HoldTerminatedException`; Expired hold cannot be Released
- `InventoryItem.HeldQuantity`: computed (`TotalQuantity - AvailableQuantity`), never stored

**Verification:** `dotnet test` → Passed: 17, Failed: 0

---

## Phase 3 — MongoDB Infrastructure (TDD)

**Goal:** Implement the repository interfaces from Phase 2 against MongoDB, wire DI, create indexes and seed data. All tests mock `IMongoCollection<T>` — no Docker required.

**Key design decision — Domain/Infrastructure boundary:**
Domain entities have `private set` and no BsonAttributes. Options: (a) add BsonAttributes to domain (leaks MongoDB.Driver into Domain project), (b) create separate Infrastructure document models. Chose (b): `HoldDocument`, `InventoryDocument`, `AppSettingDocument` in `Infrastructure/Persistence/Documents/` carry all BsonAttributes; repositories map to/from domain entities.

**Domain patch:** Added `Hold.Reconstruct(...)` static method — reconstructs a `Hold` from stored data without re-running business validation. Works because a `static` method inside `Hold` can access `private Hold()` constructor and `private set` properties via object initializer.

**Transaction boundary:** `MongoTransaction` wraps `IClientSessionHandle`; exposes it `internal` so repositories in the same assembly extract it via `(t as MongoTransaction)?.Session` — MongoDB types never leak into Domain.

**Files created (12 total):**

| Layer | Files |
|-------|-------|
| Domain/Entities/ | `Hold.cs` (patch: +Reconstruct) |
| Infrastructure/Persistence/Documents/ | `HoldDocument.cs` (+HoldItemDocument), `InventoryDocument.cs`, `AppSettingDocument.cs` |
| Infrastructure/Persistence/ | `CollectionIndexInitializer.cs`, `DatabaseSeeder.cs`, `MongoHoldRepository.cs`, `MongoInventoryRepository.cs`, `MongoSettingsRepository.cs` |
| Infrastructure/Transactions/ | `MongoTransaction.cs`, `MongoTransactionFactory.cs` |
| UnitTests/Infrastructure/ | `DatabaseSeederTests.cs`, `MongoInventoryRepositoryTests.cs`, `MongoHoldRepositoryTests.cs` |

**Verification:** `dotnet test` → Passed: 31, Failed: 0 (17 Phase 2 + 14 Phase 3)

**Pending:** 3.5.2 — manual verify via mongosh after `docker-compose up -d` (indexes + 5 seed products)

---

## Phase 4 — POST /api/holds (TDD)

**Goal:** The riskiest endpoint fully working — multi-document transaction, write conflict retry (3× with 50ms delay), all-or-nothing stock validation with aggregated `failures[]`, and RFC 7807 ProblemDetails error mapping.

**Key design decisions:**
- `HoldService` placed in `WebApi/Services/` — no separate Application project (deliberate simplification for 5-project assignment; UnitTests.csproj already referenced WebApi)
- `DomainExceptionHandler` implements `IExceptionHandler` (.NET 8+ DI-registered handler) — cleaner than lambda-based `UseExceptionHandler`
- DTOs (`CreateHoldRequest`, `HoldResponse`) kept as pure records in `Contracts` with no Domain dependency — mapping logic in endpoint
- `ProductNotFoundException` and `StockUnavailableException` added to Domain so middleware has a consistent exception hierarchy to match

**TDD flow:**
1. Wrote `CreateHoldServiceTests.cs` (8 tests, RED — `HoldService` didn't exist)
2. Implemented `HoldService.cs` (GREEN — all 8 pass)
3. Wired middleware, endpoint, Program.cs

**Files created (8 total):**

| Layer | Files |
|-------|-------|
| Domain/Exceptions/ | `ProductNotFoundException.cs`, `StockUnavailableException.cs` |
| Contracts/Requests/ | `CreateHoldRequest.cs` |
| Contracts/Responses/ | `HoldResponse.cs` |
| WebApi/Services/ | `HoldService.cs` |
| WebApi/Middleware/ | `DomainExceptionHandler.cs` |
| WebApi/Endpoints/ | `HoldEndpoints.cs` |
| UnitTests/Application/ | `CreateHoldServiceTests.cs` (8 tests) |

**Non-obvious bug caught during execution:**
`when (e.Code == 112)` exception filter silently evaluates to `false` when `MongoCommandException.Code` property throws internally (BsonDocument key access in MongoDB.Driver 3.9.0). Exception filters that throw are treated as `false` in C# — no error, just silent skip. Fixed by reordering: check `e.Message.Contains("WriteConflict")` first (short-circuits before `e.Code` is evaluated). Production MongoDB write conflict responses always include "WriteConflict" in the error message, so no behavior change in production.

**Infrastructure fix required:** MongoDB standalone does not support multi-document transactions. `docker-compose.yml` updated to start MongoDB as a single-node replica set (`--replSet rs0`). Healthcheck updated to auto-run `rs.initiate()` on first boot. Existing standalone volume wiped (`docker-compose down -v`) and recreated. Seeder re-ran automatically on next `dotnet run`.

**Verification:** `dotnet test` → Passed: 39, Failed: 0 (31 Phase 2+3 + 8 Phase 4)

**Manual test results (4.2.5) — all 5 scenarios verified via Scalar UI:**

| # | Scenario | Request | Expected | Result |
|---|----------|---------|----------|--------|
| 1 | Happy path | `POST /api/holds` — widget-a qty 3, customerName Alice | 201 Created, Location header, status Active, expiresAt +15 min, productName "Widget A" | ✅ |
| 2 | Insufficient stock | widget-a qty 999 | 409, `data.failures[0]` with productId / requested: 999 / available: 50 | ✅ |
| 3 | Product not found | productId "does-not-exist" qty 1 | 404, title containing "not found" | ✅ |
| 4 | Validation error | empty items array | 422, title "Hold must have at least one item." | ✅ |
| 5 | Inventory decrement | widget-a qty 49 after hold of qty 3 | 409 insufficient stock (47 remaining, not 50) | ✅ |

---

## Phase 5 — Background Worker (TDD)

**Files created:** `WebApi/Stubs/NullHoldEventPublisher.cs`, `WebApi/Stubs/NullInventoryCache.cs`, `WebApi/Workers/HoldExpiryWorker.cs`, `UnitTests/Application/HoldExpiryWorkerTests.cs`
**Files modified:** `WebApi/Program.cs`

**Key design decisions:**
- `ProcessExpiredHoldsAsync` is `public` on `HoldExpiryWorker` — allows direct test calls without spinning up the `BackgroundService` lifecycle (no need for `IHostedService.StartAsync` + CancellationToken plumbing in tests)
- Delay-first loop in `ExecuteAsync` — worker waits 30s before first poll, ensuring seed data is ready before any expiry queries run at startup
- `when (!stoppingToken.IsCancellationRequested)` exception filter — `OperationCanceledException` on graceful shutdown bypasses the catch and exits the loop naturally; only genuine errors are logged
- Null stubs (`NullHoldEventPublisher`, `NullInventoryCache`) registered as `AddSingleton` — replaced by real implementations in Phases 8 (RabbitMQ) and 9 (Redis) with a one-line swap in Program.cs
- Cache invalidated once per tick (not per hold) and only if ≥1 transition succeeded — avoids unnecessary Redis writes when all holds lost the race to DELETE

**Race condition design:**
`AtomicTransitionAsync` uses MongoDB `FindOneAndUpdate` with filter `{ _id, status: Active }`. If DELETE already transitioned the hold, the filter matches nothing → returns `null`. Worker checks `if (result is null) continue` — skips both `IncrementAsync` and `PublishHoldExpiredAsync`. No double-restore of inventory is possible.

**Additional logging added:** `LogInformation` on each successful expiry + tick summary; `LogDebug` on empty ticks and race-lost skips. Worker namespace log level set to `Debug` in `appsettings.Development.json` for visibility during development.

**Infrastructure fix:** `appsettings.Development.json` corrected to use `localhost:27017` / `localhost:6379` / `localhost` for all services — the base `appsettings.json` uses Docker hostnames (`mongodb`, `redis`, `rabbitmq`) for container-to-container networking; the Development override is required when running the API locally against Docker-exposed ports.

**Verification:** `dotnet test` → **Passed: 43, Failed: 0** (39 Phase 2+3+4 + 4 Phase 5)

**Manual test (5.7) — verified:** Set `ExpirationMinutes: 1`, created hold (widget-a qty 5), waited ~2 min, confirmed `status: "Expired"` in MongoDB and `availableQuantity` restored to baseline. ✅

---

## Phase 6 — GET & DELETE Endpoints (TDD)

**Files created:** `Contracts/Responses/PagedResponse.cs`, `UnitTests/Application/GetHoldServiceTests.cs`, `UnitTests/Application/ReleaseHoldServiceTests.cs`, `UnitTests/Application/ListHoldsServiceTests.cs`
**Files modified:** `WebApi/Services/HoldService.cs`, `WebApi/Endpoints/HoldEndpoints.cs`, `UnitTests/Application/CreateHoldServiceTests.cs`

**Key design decisions:**
- `IInventoryCache` added as last constructor param to `HoldService` — already registered as `NullInventoryCache` in Phase 5, so no Program.cs changes needed
- `ReleaseHoldAsync` reuses `Hold.MarkReleased()` for the 404/410 distinction — when `AtomicTransitionAsync` returns null, we fetch the hold and call `MarkReleased()` on it. If already Released/Expired, the domain method throws `HoldTerminatedException` with the correct `At` timestamp. If not found, we throw `HoldNotFoundException`. No duplication of status-check logic in the service.
- `ToResponse(Hold)` private static helper extracted in `HoldEndpoints` — avoids repeating the 8-field mapping across all 4 endpoints
- List endpoint: `status ?? "active"` default applied at endpoint level (not service) — keeps `ListHoldsAsync` signature clean and testable with explicit status strings
- `pageSize` validation in service (not endpoint) — tested without HTTP infrastructure

**Compile fix logged:** `MapGet` lambda with `int page = 1, int pageSize = 20` before `HoldService service` triggered CS1737. C# requires optional parameters after all required ones — reordered to DI params first, then defaulted query params.

**Verification:** `dotnet test` → **Passed: 54, Failed: 0** (43 Phase 2–5 + 11 Phase 6)

**Pending:** 6.3.4 — manual verify all 3 endpoints via Scalar UI

---

## Phase 7 — GET /api/inventory + POST /api/inventory/reset (TDD)

**Files created:** `Contracts/Responses/InventoryItemResponse.cs`, `UnitTests/Application/GetInventoryServiceTests.cs`, `UnitTests/Application/ResetInventoryServiceTests.cs`, `WebApi/Services/InventoryService.cs`, `WebApi/Endpoints/InventoryEndpoints.cs`
**Files modified:** `Domain/Repositories/IHoldRepository.cs`, `Infrastructure/Persistence/MongoHoldRepository.cs`, `WebApi/Program.cs`

**Key design decisions:**
- New `InventoryService` instead of adding to `HoldService` — inventory and hold operations are separate resources; keeps each service focused and independently testable
- `IHoldRepository.DeleteAllAsync` added — not present in original interface, needed for reset to wipe all holds before restoring inventory quantities
- `ResetInventoryAsync` reads from DB (not cache) after flush — `FlushAllAsync` invalidates the cache, so we call `GetAllAsync` directly to return the freshly-reset state. Third test (`DoesNotReadCacheAfterFlush`) enforces this.
- `HeldQuantity` is a computed domain property (`TotalQuantity - AvailableQuantity`) on `InventoryItem` — endpoint maps it directly, no service-layer computation needed

**Verification:** `dotnet test` → **Passed: 60, Failed: 0** (54 Phase 2–6 + 6 Phase 7)

**Manual test (7.6) — verified:** Created hold (widget-a qty 5) → GET /api/inventory confirmed `heldQuantity: 5`, `availableQuantity` reduced → POST /api/inventory/reset returned all products with `availableQuantity = totalQuantity`, `heldQuantity: 0` → GET /api/holds confirmed empty. ✅

---

## Phase 8 — RabbitMQ Publisher (TDD)

**Files created:** `Contracts/Events/EventItem.cs`, `HoldCreatedEvent.cs`, `HoldReleasedEvent.cs`, `HoldExpiredEvent.cs`, `Infrastructure/Messaging/RabbitMqConnectionFactory.cs`, `RabbitMqTopologyInitializer.cs`, `RabbitMqHoldEventPublisher.cs`, `UnitTests/Infrastructure/RabbitMqPublisherTests.cs`
**Files modified:** `WebApi/Services/HoldService.cs`, `WebApi/Program.cs`, `UnitTests/Application/CreateHoldServiceTests.cs`, `ReleaseHoldServiceTests.cs`, `GetHoldServiceTests.cs`, `ListHoldsServiceTests.cs`

**Key design decisions:**
- Event DTOs in `Contracts/Events/` (not Infrastructure) — outbound message shapes are contracts, not infrastructure details
- One channel per publish — channels are lightweight and short-lived in RabbitMQ; avoids channel state management and thread-safety issues
- Fire-and-forget via try/catch in `PublishAsync` private method — publish errors are logged at Error level but never rethrow. HTTP response and DB transaction are already complete before publish, so caller is unaffected
- `IHoldEventPublisher` + `ILogger<HoldService>` added as last two constructor params to `HoldService` — DI injects both; tests pass `_publisher.Object` + `NullLogger<HoldService>.Instance`
- `HoldExpiryWorker` already called `PublishHoldExpiredAsync` — no wiring change needed there
- `RabbitMqTopologyInitializer` runs at startup after `DatabaseSeeder` — exchange and queues declared idempotently (safe to rerun)
- `IConnection` registered as singleton via `GetAwaiter().GetResult()` in DI factory — acceptable at startup before requests begin

**Verification:** `dotnet test` → **Passed: 66, Failed: 0** (60 Phase 2–7 + 4 RabbitMqPublisher + 2 wiring)

**Diagnostic logging added to `RabbitMqTopologyInitializer`:** During manual verification, the exchange and queues were not visible in the Management UI after first start. Root cause: the first `dotnet run` used the old binary (before the logging commit); on re-run with logging, the topology initializer confirmed it ran successfully (`amqp://localhost:5672`, exchange declared, all 3 queues bound). Also identified that the `appsettings.json` base config correctly uses `"Host": "rabbitmq"` (Docker-to-Docker) while `appsettings.Development.json` overrides to `"Host": "localhost"` for local dev — configuration layering is intentional.

**Manual verification (8.10) — ✅ Verified:**
- Topology initializer logs at startup: exchange + 3 queues declared on `amqp://localhost:5672`
- `POST /api/holds` → `hold.created.queue` receives message with correct JSON (holdId, customerName, status, items[], createdAt, expiresAt)
- `HoldExpiryWorker` expiry tick → `hold.expired.queue` receives message with expiredAt
- Fire-and-forget confirmed: no publish errors propagate to HTTP caller

---

## Phase 9 — Redis Caching (TDD)

**Files created:** `Infrastructure/Caching/RedisCacheService.cs`, `UnitTests/Infrastructure/RedisCacheServiceTests.cs`
**Files modified:** `WebApi/Services/HoldService.cs`, `WebApi/Program.cs`, `UnitTests/Application/CreateHoldServiceTests.cs`

**Key design decisions:**
- `HoldCacheDto` + `HoldItemCacheDto` internal records live in same file as `RedisCacheService` — `Hold` has `private set` properties that STJ cannot deserialize directly; DTOs bridge the gap without polluting the Domain layer
- `Hold.Reconstruct(...)` from Phase 3 used in `GetHoldAsync` — rehydrates domain object from DTO fields without re-running invariant validation
- `FlushAllAsync` deletes only the 2 static keys (`inventory:all`, `settings:expiration-minutes`); individual `hold:{id}` keys expire via their 60s TTL — avoids `IServer` dependency for `FLUSHDB`
- Settings cache-first in `CreateHoldAsync` — on cache hit, skips MongoDB query for expiration minutes entirely
- `InvalidateInventoryAsync` called immediately after successful `AttemptCreateAsync` — ensures next `GET /api/inventory` reflects the decremented available quantities

**SE.Redis v3 breaking change encountered:**
`StringSetAsync` in v3.0.x changed signature from `(key, value, TimeSpan? expiry, bool keepTtl, When when, CommandFlags flags)` (6 params) to `(key, value, Expiration expiry, When when, CommandFlags flags)` (5 params, `Expiration` struct absorbs `keepTtl`). Moq `Callback<..., TimeSpan?, bool, ...>` silently failed because the overload didn't match. Resolved via `_db.Invocations` to read actual arguments post-call, casting `Arguments[2]` as `Expiration` for TTL assertion.

**TTL values:**
| Key pattern | TTL |
|-------------|-----|
| `inventory:all` | 30s |
| `hold:{id}` | 60s |
| `settings:expiration-minutes` | 60s |

**Verification:** `dotnet test` → **Passed: 79, Failed: 0** (66 Phase 2–8 + 13 Phase 9)

---

## Phase 10 — Health Checks + Swagger Annotations

**Files created:** `WebApi/HealthChecks/RabbitMqHealthCheck.cs`, `UnitTests/Infrastructure/RabbitMqHealthCheckTests.cs`
**Files modified:** `WebApi/Program.cs`, `WebApi/Endpoints/HoldEndpoints.cs`, `WebApi/Endpoints/InventoryEndpoints.cs`

**Key design decisions:**
- `RabbitMqHealthCheck` uses `IConnection.IsOpen` — lightweight, no network round-trip, consistent with how the RabbitMQ client exposes connection state
- Response writer: `{"status":"Healthy"}` when all pass; only adds `"checks":{...}` detail when degraded/unhealthy — keeps the happy-path response minimal for monitoring systems
- MongoDB and Redis health checks use factory overload (`sp => sp.GetRequiredService<T>()`) to reuse the already-registered singletons — avoids a second connection
- `AddMongoDb(connectionString, tags)` fails in v9 — second param is `Func<IServiceProvider, IMongoClient>?`, not tags; factory overload is the correct approach

**Verification:** `dotnet test` → **Passed: 82, Failed: 0**

## Phase 11 — Test Suite Verification

All 5 mandatory scenarios confirmed covered:

| Scenario | Test method |
|----------|------------|
| Validation → 422 | `CreateHoldAsync_EmptyItems_ThrowsDomainException` |
| Happy path → 201 | `CreateHoldAsync_AllInStock_ReturnsHoldWithDenormalizedProductName` |
| Insufficient stock → 409 | `CreateHoldAsync_InsufficientStock_ThrowsWithAllFailures` |
| Write conflict retry | `CreateHoldAsync_WriteConflict_RetriesThreeTimesAndThrows` |
| Race condition | `ProcessExpiredHoldsAsync` race condition test |

Zero tests require Docker — all infrastructure mocked via Moq.

---

## Phase 12 + 13 — React Frontend + Docker Integration (completed 2026-06-28)

**Goal:** React SPA demonstrating the full hold lifecycle (create → view → countdown → release/expire). Single `docker-compose up --build` starts all 5 services.

**Planning approach:**
- Used Claude Code plan mode; AI read all API contract types (exact camelCase field names confirmed), docker-compose.yml, design-discussion.md, and progress.md
- Key architecture decisions: TanStack Query v5 (server state only), Zustand (UI state + toast queue), Axios interceptor pattern (`useStore.getState()` works outside React)
- Filter "show all": user chose to omit `status` param entirely (backend was `?? "active"` — 1-line fix to pass null through; repo already handled null → all docs)

**Test strategy (TDD-light):**
- Vitest + @testing-library/react + happy-dom (switched from jsdom due to `html-encoding-sniffer` → `@exodus/bytes` ESM conflict)
- 11 component tests across 4 test files; all mocks via `vi.mock` + `vi.hoisted(() => vi.fn())`
- `renderWithProviders` uses `{ wrapper: Wrapper }` pattern so `rerender` auto-wraps in `QueryClientProvider`

**Files created (28 new files):**

| Area | Files |
|------|-------|
| Config | `frontend/vite.config.ts` (vitest inline), `frontend/src/setupTests.ts` |
| Types | `frontend/src/types/api.ts` |
| API | `frontend/src/api/client.ts`, `inventory.ts`, `holds.ts` |
| Store | `frontend/src/store/useStore.ts` |
| Shared | `ErrorBanner.tsx`, `Toast.tsx`, `LoadingSkeleton.tsx`, `LoadingSpinner.tsx` |
| InventoryDashboard | `InventoryDashboard.tsx`, `InventoryDashboard.module.css`, `InventoryDashboard.test.tsx` |
| CreateHoldForm | `CreateHoldForm.tsx`, `CreateHoldForm.module.css`, `CreateHoldForm.test.tsx` |
| HoldsList | `HoldsList.tsx`, `HoldCard.tsx`, `HoldsList.module.css`, `HoldsList.test.tsx`, `HoldCard.test.tsx` |
| App | `App.tsx`, `App.css`, `main.tsx`, `test-utils.tsx` |
| Docker | `api/Dockerfile`, `frontend/Dockerfile`, `frontend/nginx.conf` |
| docker-compose | `api` + `frontend` services added |

**Backend fix:** `HoldEndpoints.cs` — removed `status ?? "active"` so `GET /api/holds` with no status param returns all holds (repo `GetPagedAsync` already used `FilterDefinition.Empty` for null status).

**Verification:** `npx vitest run` → **11/11 passed** · `dotnet test` → **82/82 passed**

**Non-obvious issues encountered:**
1. `rolldown` Win32 native binding not installed by npm optional-dep bug — explicit package install required
2. `jsdom` ESM conflict — switched to `happy-dom`; identical API, no config change
3. `vi.mock` hoisting: top-level `const` in TDZ when factory runs — `vi.hoisted()` or inline data required
4. RTL `rerender` drops providers — fixed by `render(ui, { wrapper: Wrapper })` pattern
5. Zustand store is a singleton across tests — `beforeEach(() => useStore.setState({...}))` required
6. CSS Modules mangle class names in test env — `.toMatch(/held/)` regex beats exact `.toHaveClass()`

---

## Human Audit

This section records every design decision made during the Q&A phase and every implementation-phase correction. The Q&A decisions are the load-bearing ones — they determined the entire system shape before a single line of code was written.

---

### Part 1 — Architecture Decisions (Q&A Phase)

#### Concurrency & Race Conditions

**Q1 — Multi-item (cart-level) hold over single-product hold**
AI offered both options. Chosen: one hold covers multiple products — a checkout-level atomic unit. Consequence: multi-document MongoDB transactions are required (a single-document `findOneAndUpdate` is insufficient). This raised the implementation complexity bar significantly but reflects real commerce semantics.

**Q8 — All-or-nothing stock validation, not partial holds**
AI offered a partial-fill alternative (hold what's available, skip the rest). Rejected. Chosen: atomic rollback with `409 Conflict` returning a per-item failure list (`productId`, requested qty, available qty). A partial hold that silently skips items would confuse the customer about what they actually have reserved.

**Q23 — Atomic `findOneAndUpdate` with `status: "Active"` guard to prevent double inventory restoration**
AI presented a distributed lock as one option. Rejected. Chosen: filter on `{ _id, status: "Active" }` means only one caller — the worker or the client DELETE — can win the status transition. The loser gets `null` back and skips inventory restore and event publish. No lock, no coordination overhead, no distributed state.

**Q26 — Retry write conflicts 3× with 50ms backoff before returning 409**
AI presented immediate 409 on first conflict as the simpler option. Rejected. Under burst traffic, two concurrent requests for the last unit will both see sufficient stock inside their transactions, but only one can commit. Without retry, the first collision always produces a 409 that the user didn't deserve. Retry gives the transaction a second attempt with the correct post-commit stock view.

---

#### API Contract & HTTP Semantics

**Q4/Q5/Q15/Q29/Q30/Q31 — HTTP code matrix for hold states**
AI initially proposed a single 404 for all "not found or gone" cases. Rejected. Final matrix:

| Scenario | Code | Reasoning |
|---|---|---|
| GET expired/released hold | 200 with `status` field | Reads always return the document; status field carries meaning |
| DELETE on expired hold | 410 Gone | Terminal state — inventory already restored, don't retry |
| DELETE on released hold | 410 Gone | Same: terminal, consistent client logic |
| DELETE/GET non-existent holdId | 404 Not Found | Never existed vs existed-but-gone are semantically distinct |
| DELETE success | 200 with released hold document | Frontend updates card immediately without a follow-up GET |

**Q12 — RFC 7807 ProblemDetails throughout, not a custom envelope**
AI offered a custom JSON error envelope. Rejected. ProblemDetails is the .NET 10 standard, reviewers will recognise it, and it supports extension fields for domain-specific data (the `failures[]` array on 409).

**Q2 — Add `GET /api/holds` as an implicit 5th endpoint**
The spec listed 4 endpoints. AI accepted the spec literally. Rejected. A frontend that tracks hold IDs in memory is fragile and untestable. Added list endpoint with `?status=` filter and pagination.

---

#### Data Model

**Q3 — Materialized `availableQuantity` on inventory documents, not computed from holds**
AI suggested computing available stock on every read by joining holds. Rejected. A join on every inventory read is O(n × m). Chosen: `availableQuantity` is a stored, atomic-increment field. `POST /api/holds` → `$inc -N`. Worker/DELETE → `$inc +N`. `GET /api/inventory` is a pure read with no joins. `heldQuantity` (Q10) is then `totalQuantity - availableQuantity` computed at read time — no storage needed.

**Q6 — Hold expiration stored in MongoDB settings collection, not appsettings.json only**
AI defaulted to appsettings.json. Rejected for runtime mutability. Chosen: read from a `settings` MongoDB collection first, fall back to `HoldSettings.ExpirationMinutes` from appsettings if absent. Cached in Redis at 60s TTL to avoid a DB hit on every hold creation.

**Q25 — GUID string for hold `_id`, not MongoDB ObjectId**
AI defaulted to ObjectId. Rejected. GUID: doesn't expose DB technology in URLs, is standard in .NET, generatable before insert (useful for idempotency), readable in logs. Inventory documents keep ObjectId internally — no client-visible impact.

**Q36 — All timestamps `DateTime.UtcNow`, `DateTime.Now` forbidden**
Not in the spec. Decided proactively before any code was written. All 4 hold timestamps (`createdAt`, `expiresAt`, `releasedAt`, `expiredAt`) use UTC. Enforced as a codebase-wide rule.

---

#### Caching

**Q13 — Cache `GET /api/inventory` and `GET /api/holds/{id}`, but NOT the holds list**
AI initially suggested caching all read endpoints. The holds list was specifically excluded. Reason: the list changes every 30 seconds from the background worker plus on every create/release mutation. Cache invalidation on every write adds overhead that outweighs the read benefit. Direct MongoDB reads against the `{status, createdAt}` index is sufficient.

**Q35 — Only invalidate Redis if holds actually expired (not on every worker tick)**
AI generated unconditional `InvalidateInventoryAsync()` in the worker. Rejected. If a tick finds zero expired holds, there is nothing to invalidate — no inventory changed. Conditional invalidation: count transitioned holds; call Redis only if `transitioned > 0`.

---

#### Infrastructure & Messaging

**Q11 — Direct exchange with three queues, one per event type**
AI offered topic exchange as an alternative. Rejected for this scope. Direct exchange is the simplest topology that correctly routes each event type to independent consumer queues. Topic exchange adds routing-key wildcards that would be unnecessary complexity without multiple consumer groups per event.

**Q16 — Fire-and-forget for RabbitMQ publish failures**
AI offered fail-the-request as one option. Rejected. The HTTP response and DB transaction are complete before publish — rolling back the DB because a message broker had a transient hiccup is wrong. Chosen: log at Error level, continue. Documented trade-off: production would use the Transactional Outbox Pattern for at-least-once delivery guarantees.

**Q21 — `/health` with per-dependency status (MongoDB + Redis + RabbitMQ)**
AI offered a stub `/health → 200` with no actual dependency checks. Rejected. The health endpoint is the `depends_on: condition: service_healthy` signal in docker-compose — a stub that always returns 200 removes the only thing that prevents the API from starting before MongoDB initialises its replica set.

**Q32 — Publishers only, no consumer implementation**
Confirmed scope boundary. This service publishes events with sufficient payload for downstream consumers to act without secondary DB lookups (`items[]` included in all events, per Q27). No consumer workers in this repo.

---

#### Frontend

**Q14 — TanStack Query for server state, Zustand for UI state only**
AI offered Redux as an option. Rejected as overkill. AI also offered Zustand-only. Rejected — Zustand isn't designed for server state; you'd manually reimplement caching, background refetch, and mutation status. Chosen split: TanStack Query owns all API data, Zustand owns filter toggle, current page, and toast queue.

**Q17 — Client-computed countdown from `expiresAt` timestamp**
AI offered server-computed `remainingSeconds`. Rejected — stale immediately after the response is received. Client reads `expiresAt` from the API response, runs a `setInterval`, and refetches from TanStack Query when the timer hits zero to confirm expiry status.

**Q19 — Frontend in docker-compose, not npm run dev separately**
AI offered both. Chosen: single `docker-compose up --build` starts the full stack. Multi-stage Dockerfile: Node build (Vite) → Nginx serve. Nginx reverse-proxies `/api/*` to the .NET API container — no CORS issues, no hardcoded localhost ports in the React bundle.

**Q37 — Inline errors for domain errors, toast for network/500 errors**
Chosen pattern: 409/404/422 API errors display inline in the form or section where the action originated (using ProblemDetails `detail` message directly). Network failures and 5xx errors show a dismissable toast — non-blocking, no modal required.

---

#### Operations

**Q22 — Seed only on empty collection; expose `POST /api/inventory/reset` for reviewer resets**
AI offered always-seed-on-startup. Rejected — it would wipe demo state on every container restart. Chosen: check `count == 0` before seeding. Reset endpoint added for reviewers to restore initial quantities without `docker-compose down -v`.

**Q34 — Hard cap `pageSize` at 100, return 422 if exceeded**
Not in the spec. AI left `pageSize` unbounded. Rejected — `?pageSize=999999` is a trivial resource exhaustion vector. Cap enforced at the service layer with `422 Unprocessable Entity`.

**Q24 — Explicit index decisions before any code**
AI left indexes as a Phase 3 afterthought. Moved to pre-implementation decision:
- `holds: { status: 1, expiresAt: 1 }` — background worker query
- `holds: { status: 1, createdAt: -1 }` — list endpoint sort
- `inventory: { productId: 1 }` unique — hold creation validation lookup

---

### Part 2 — Implementation-Phase Corrections

These are cases where AI-generated code was wrong due to environment assumptions or version-specific behaviour, caught during build or test.

**Rejected — `addgroup`/`adduser` in Dockerfile**
AI used BusyBox/Alpine commands. `mcr.microsoft.com/dotnet/aspnet:10.0` is Debian Bookworm — these commands don't exist. Fixed to `groupadd`/`useradd`.

**Rejected — MongoDB RS member as `localhost:27017`**
Inside Docker, `localhost` is the container itself. API container resolving `localhost:27017` connects to itself, not MongoDB. Fixed to `host:'mongodb:27017'` plus `?directConnection=true` in the MongoDB URI to skip topology re-resolution.

**Rejected — Healthcheck using `wget`**
`mcr.microsoft.com/dotnet/aspnet:10.0` ships without `wget` or `curl`. Healthcheck exited 127 and the container was forever unhealthy. Fixed: `apt-get install -y curl` in Dockerfile, `["CMD", "curl", "-sf", "http://localhost:8080/health"]` in docker-compose.

**Rejected — `@rolldown/binding-win32-x64-msvc` in hard `dependencies`**
npm refuses Windows-only packages on Linux (`EBADPLATFORM`). Moved to `optionalDependencies` so Linux Docker containers skip it without build failure.

**Rejected — Frontend on port 80**
Port 80 requires admin elevation on Windows. Fixed to `3000:80`.

**Accepted — `directConnection=true` to bypass RS topology redirect**
After identifying the MongoDB redirect root cause, AI proposed this flag. Accepted as the idiomatic .NET MongoDB driver solution — tells the driver to treat the URI host as a direct endpoint, no topology discovery.

**Accepted — Separate `vitest.config.ts`**
`/// <reference types="vitest" />` doesn't load in Docker's `tsc -b` context when `tsconfig.node.json` has no vitest in its `types` array. AI proposed splitting into `vite.config.ts` (build only) and `vitest.config.ts` (imports vitest's own `defineConfig`, which types `test` natively). Accepted.

**Accepted — `HoldService` and `InventoryService` in `WebApi/Services/`, not Domain**
Spec suggests `Domain/Services/`. AI proposed WebApi layer. Accepted: Domain stays infrastructure-free and independently testable. Application services (orchestrating domain + infrastructure) are a WebApi concern.

---

## Verification

### Test generation

Unit tests were generated by AI alongside each implementation phase (TDD order: test first, then implementation). For each component, AI was given:
- The interface it should test (repository interface, domain entity, service)
- The exact scenario (happy path, InsufficientStock, HoldNotFound, HoldTerminated, concurrent retry)
- The Moq setup pattern already established in the project

AI generated test scaffolding including mock setup, `Act`, and `Assert`. Human review validated:
- Mock method names matched the actual interface (verified by running `dotnet test` — compile errors catch typos)
- Assert conditions matched the actual domain exception type, not a generic one
- Edge case tests (concurrent retry exhaustion, write conflict counting) used the exact error code that MongoDB returns

Final count: **82 unit tests, 0 failures, 0 infrastructure dependencies.**

---

### Validation of AI-generated concurrency code

The most critical AI-generated code was the atomic inventory decrement with write-conflict retry in `HoldService`. AI generated the retry loop with `catch (MongoException ex) when (ex.Message.Contains("WriteConflict"))`. This was validated two ways:

1. **Unit test**: `HoldService_RetryExhausted_Returns409` — mocked repository throws `MongoException("WriteConflict")` three consecutive times; asserted `InsufficientStockException` thrown after retry limit. Passed.

2. **Live concurrency test**: Two simultaneous `POST /api/holds` requests for `device-z` (stock: 10), each requesting qty 7. Expected: one 201, one 409 with `available: 3`. Result: exactly that — atomic transition confirmed, no double-decrement, `available` accurately reflects post-first-hold stock.

---

### Validation of background worker expiry

AI generated `HoldExpiryWorker` — a `BackgroundService` that polls MongoDB every 30 seconds and uses `FindOneAndUpdate` with `{ status: Active, expiresAt ≤ now }` to atomically transition holds. Validated end-to-end:

1. Created hold with `ExpirationMinutes: 1`
2. Waited 95 seconds (TTL + worst-case poll window + buffer)
3. `GET /api/holds/{id}` → `status: "Expired"`, `expiredAt` populated
4. `DELETE /api/holds/{id}` → 410 Gone with `data.at = expiredAt`
5. `GET /api/inventory` → `heldQuantity: 0` (inventory fully restored by worker)

Worker latency observed: 24 seconds after `expiresAt` — within the 30-second poll window.

---

### Docker stack validation

After each infrastructure change, `docker-compose up --build` was run from a cold state and all 5 services verified healthy:
- mongodb: healthcheck runs `rs.status()` / `rs.initiate()` — confirmed RS member is `mongodb:27017`
- redis: `redis-cli ping` → PONG
- rabbitmq: `rabbitmq-diagnostics ping` → management UI reachable at :15672
- api: `curl -sf http://localhost:8080/health` → `{"status":"Healthy"}`
- frontend: nginx serves SPA at :3000, proxies `/api/*` to `api:8080`

Total Docker build failures diagnosed and fixed: **6** (addgroup/useradd, EBADPLATFORM, TS2769, MongoDB localhost redirect, wget missing, port 80 elevation). All fixes verified by re-running `docker-compose up --build` to a clean healthy state.

---

### RabbitMQ event verification

AI generated event payloads for `HoldCreated`, `HoldReleased`, `HoldExpired`. Validated via RabbitMQ Management API (`GET /api/queues`):

| Queue | Messages after full QA run |
|-------|---------------------------|
| hold.created.queue | 7 |
| hold.released.queue | 1 |
| hold.expired.queue | 2 |

Counts match the number of create/release/expire operations performed during QA — no missing events, no duplicate publishes.
