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

**Pending:** 7.6 — manual verify: create hold → GET /api/inventory (heldQty > 0) → POST /api/inventory/reset → GET /api/holds (empty)

---

## Human Audit
*(Specific examples of AI suggestions accepted and rejected — to be documented during development)*

---

## Verification
*(How AI was used to generate tests and how AI-generated code was validated — to be documented during development)*
