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

## Human Audit
*(Specific examples of AI suggestions accepted and rejected — to be documented during development)*

---

## Verification
*(How AI was used to generate tests and how AI-generated code was validated — to be documented during development)*
