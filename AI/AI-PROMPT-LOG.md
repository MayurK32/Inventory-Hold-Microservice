# AI-PROMPT-LOG.md
## Inventory Hold Microservice — Prompt Engineering Log

This file tracks all significant prompts, their purpose, and outcomes.

---

## Session 1 — 2026-06-27: Requirements Analysis & Architecture Design

### PROMPT-001
**Phase:** Pre-implementation — Requirements Clarification
**Tool:** Claude Code (claude-sonnet-4-6, Max effort)
**Purpose:** Act as senior architect to read the assignment PDF and conduct structured Q&A to surface all ambiguities before writing a single line of code.
**Context Given:**
- Full assignment PDF (Senior Assignment - Dotnet Full Stack)
- Full JD PDF (Job Description Senior Full Stack Engineer)
**Prompt Summary:**
> "Act as a senior architect with 10+ years experience in commerce applications... read both documents and ask as many questions as you can 1 by 1 about the requirement... First create AI-USAGE.md and each question with answer will go inside it..."
**Outcome:** Initiated structured Q&A session. Files created. Questions being asked sequentially.

---

### PROMPT-002
**Phase:** Pre-implementation — Gap Analysis
**Tool:** Claude Code (claude-sonnet-4-6, Max effort)
**Purpose:** Cross-reference all 23 Q&A decisions against the full assignment spec to find critical gaps before writing any code. Spawned 2 parallel Explore agents — one for spec vs decisions mapping, one for data modeling and concurrency gaps.
**Context Given:**
- Full design-discussion.md (23 decisions)
- Assignment spec requirements (all 7 sections)
**Prompt Summary:**
> "First scan all those 23 questions and requirement again and find if we are missing something critical, think deeply dont hallucinate"
**Outcome:** 14 critical gaps identified (Q24–Q37). Gaps covered: MongoDB indexes, document ID strategy (GUID), write conflict retry logic, RabbitMQ event payload schemas, complete document field definitions, non-existent hold HTTP codes, DELETE success response, RabbitMQ consumer scope, seed data, pagination cap, background worker cache guard, UTC timestamps, frontend error UX. All added to design-discussion.md.

---

### PROMPT-003
**Phase:** Pre-implementation — Implementation Sequencing
**Tool:** Claude Code (claude-sonnet-4-6, Max effort)
**Purpose:** Determine the correct order to build the system before starting any implementation. Avoid the common mistake of building layers in isolation without being able to test them.
**Prompt Summary:**
> "Now only list how should we approach implementation, first .net api, then mongo, then rabbitmq, then redis, then frontend etc or some other better approach, dont fully plan yet, just answer this question from senior architect perspective"
**Outcome:** Recommended vertical slice approach, riskiest path first — NOT layer by layer. Decided order:
1. docker-compose skeleton (all containers healthy)
2. Domain models + repository interfaces (pure C#)
3. MongoDB + `POST /api/holds` (hardest endpoint — multi-document transaction, write conflict retry)
4. Background worker (expiry logic)
5. Remaining API endpoints (GET, DELETE, list)
6. RabbitMQ (additive — bolt onto working flows)
7. Redis (additive — caching layer on proven reads)
8. Unit tests (once interfaces are stable)
9. Frontend (against working, tested API)
10. Nginx + docker-compose full integration

**Key insight logged:** MongoDB transaction correctness is the load-bearing wall. RabbitMQ and Redis are decorations. Frontend is furniture. Build in that order.

---

### PROMPT-004
**Phase:** Pre-implementation — Skill Installation
**Tool:** Claude Code (claude-sonnet-4-6, WebFetch + Write)
**Purpose:** Install 27 skills from the `antigravity-awesome-skills` GitHub repository into `.claude/skills/` so Claude Code can invoke them during implementation via `@skill-name` references.
**Context Given:**
- GitHub repo: `https://github.com/sickn33/antigravity-awesome-skills`
- Requirement: all skills relevant to the implementation; exclude `azure-servicebus-dotnet`
- Format: `.claude/skills/{skill-name}/SKILL.md`
**Prompt Summary:**
> "Now check repo https://github.com/sickn33/antigravity-awesome-skills check all available skills... list names of all the skills we require for implementation... Can you put them under .claude folder in relevant format? dont add /azure-servicebus-dotnet rest are fine"
**Outcome:** 27 SKILL.md files written to `D:\Kibo\.claude\skills\` — each skill fetched directly from the raw GitHub URL to avoid hallucination. Skills cover: .NET/C# backend, DDD (tactical + strategic + context mapping), microservices, Docker, API design, event sourcing, Redis/MongoDB management, clean code, error handling, performance, E2E testing, frontend (React/TanStack), and agent orchestration. Claude Code system immediately recognized all 27 skills as available.

---

---

## Session 2 — 2026-06-28: Phase 0 Infrastructure Skeleton

### PROMPT-005
**Phase:** Implementation — Phase 0 Planning
**Tool:** Claude Code plan mode (EnterPlanMode → ExitPlanMode)
**Purpose:** Plan the docker-compose infrastructure skeleton before writing any .NET code.
**Prompt Summary:**
> "Create proper plan for phase 0"
**Outcome:** Plan created covering `docker-compose.yml` (3 services), `.env`, `.gitignore`. During plan review, human questioned whether image versions were latest and compatible — AI verified and corrected to minor-pinned tags: `mongo:7.0`, `redis:7.4-alpine`, `rabbitmq:3.13-management`. Compatibility matrix with .NET 10 NuGet packages confirmed.

---

### PROMPT-006
**Phase:** Implementation — Phase 0 Execution
**Tool:** Claude Code (Write + Edit + Bash)
**Purpose:** Execute the approved Phase 0 plan — create docker-compose.yml, .env, .gitignore, commit and push.
**Prompt Summary:**
> *(Plan approved — implementation proceeded automatically)*
**Outcome:**
- `docker-compose.yml` created with 3 services, health checks, named volume
- `.env` created locally (not committed)
- `.gitignore` created
- **Human intervention:** Caught `.env` being staged for commit — corrected to `.env.example` pattern; `.env` added to `.gitignore`
- `.env.example` created and committed instead
- Committed as "Phase 0: docker-compose infrastructure skeleton"

---

## Session 3 — 2026-06-28: Phase 1 .NET Solution Scaffold

### PROMPT-007
**Phase:** Implementation — Phase 1 Planning
**Tool:** Claude Code plan mode (EnterPlanMode → ExitPlanMode)
**Purpose:** Plan the .NET 10 solution structure before scaffolding. Read hld.md, progress.md, and assignment PDF (via Explore agent) to derive exact project structure, dependency direction, NuGet packages, and config records.
**Prompt Summary:**
> "Plan phase 1 properly by checking hld.md and progress.md also base requirement dont hallucinate"
**Outcome:** Plan documented correcting 2 issues from original progress.md:
1. MongoDB.Driver/Redis/RabbitMQ.Client belong in Infrastructure only, not WebApi (transitive via project reference)
2. Swashbuckle deprecated for .NET 9+ — use `Scalar.AspNetCore` instead
Redis connection string format gotcha documented: StackExchange.Redis requires `host:port`, not `redis://host:port`.

---

### PROMPT-008
**Phase:** Implementation — Phase 1 Execution
**Tool:** Claude Code (Bash + Write)
**Purpose:** Execute the approved Phase 1 plan — create solution, 5 projects, wire references, install NuGet packages, create config records, write appsettings and Program.cs, clean boilerplate.
**Prompt Summary:**
> *(Plan approved — implementation proceeded automatically)*
**Outcome:**
- `dotnet new sln` + 5 projects created under `src/`
- All project references wired per DDD dependency direction
- 9 NuGet packages installed across Infrastructure, WebApi, UnitTests
- 4 config records created in `Contracts/Settings/`
- `appsettings.json` (Docker) and `appsettings.Development.json` (localhost) written
- `Program.cs` skeleton written with TODO stubs per phase
- Template boilerplate removed; `PlaceholderTest.cs` added
- `dotnet build` → 0 errors · `dotnet test` → Passed: 1

---

### PROMPT-009
**Phase:** Implementation — Phase 2 Planning
**Tool:** Claude Code plan mode (EnterPlanMode → ExitPlanMode)
**Purpose:** Plan the Domain Layer (TDD) phase before writing any code. Read progress.md Phase 2 tasks, database-design.md holds schema + state machine, ddd-tactical-patterns skill, and mongodb-inventory-hold skill to derive exact entity shapes, invariants, interface signatures, and test cases.
**Prompt Summary:**
> "plan phase 2"
**Outcome:** Plan written covering TDD order (tests → implementation), all 17 files to create, exact test cases for HoldItem (4 tests) and Hold (8 tests), exception hierarchy, `IMongoTransaction`/`ITransactionFactory` abstraction (to keep MongoDB types out of Domain), and all 5 interface contracts. Key design decision: repository interfaces take `IMongoTransaction?` — a pure Domain abstraction — instead of `IClientSessionHandle` (MongoDB.Driver type) to preserve layer boundaries.

---

### PROMPT-010
**Phase:** Implementation — Phase 2 Execution
**Tool:** Claude Code (Write + Edit + PowerShell)
**Purpose:** Execute the approved Phase 2 plan — TDD domain layer with zero infrastructure dependencies.
**Prompt Summary:**
> *(Plan approved — implementation proceeded automatically)*
**Outcome:**
- 2 test files: `UnitTests/Domain/HoldItemTests.cs` (8 tests), `UnitTests/Domain/HoldTests.cs` (8 tests)
- 4 exception classes in `Domain/Exceptions/`: `DomainException`, `InsufficientStockException` (with `StockFailure` record), `HoldNotFoundException`, `HoldTerminatedException`
- 5 entity files in `Domain/Entities/`: `HoldStatus` (enum), `HoldItem` (record, self-validating), `Hold` (class, factory + state machine), `InventoryItem` (class, computed `HeldQuantity`), `AppSetting` (class)
- 2 transaction abstractions in `Domain/Transactions/`: `IMongoTransaction`, `ITransactionFactory`
- 3 repository interfaces in `Domain/Repositories/`: `IHoldRepository`, `IInventoryRepository`, `ISettingsRepository`
- 1 messaging interface in `Domain/Messaging/`: `IHoldEventPublisher`
- 1 cache interface in `Domain/Cache/`: `IInventoryCache`
- `dotnet test` → **Passed: 17, Failed: 0** — no mocks, no Docker, pure in-memory

### PROMPT-011
**Phase:** Implementation — Phase 3 Planning
**Tool:** Claude Code plan mode (EnterPlanMode → ExitPlanMode)
**Purpose:** Plan the MongoDB Infrastructure (TDD) phase. Read progress.md Phase 3 tasks, mongodb-inventory-hold skill (exact BSON schemas, C# entity mappings, atomic operation patterns), and assessed the Domain/Infrastructure boundary problem — domain entities have `private set` and no BsonAttributes.
**Prompt Summary:**
> "Plan phase 3"
**Outcome:** Plan written covering: (1) `Hold.Reconstruct(...)` domain patch to allow Infrastructure to hydrate domain entities from stored BSON without re-running validation; (2) separate `HoldDocument`/`InventoryDocument`/`AppSettingDocument` models in Infrastructure with BsonAttributes — keeps MongoDB.Driver out of Domain; (3) `MongoTransaction` + `MongoTransactionFactory` implementing domain `IMongoTransaction`/`ITransactionFactory` — `Session` property exposed `internal` so repositories in same assembly can extract it without leaking to domain; (4) all repository implementations with mocked `IMongoCollection<T>` tests.

---

### PROMPT-012
**Phase:** Implementation — Phase 3 Execution
**Tool:** Claude Code (Edit + Write + PowerShell)
**Purpose:** Execute the approved Phase 3 plan — MongoDB infrastructure layer with TDD.
**Prompt Summary:**
> *(Plan approved — implementation proceeded automatically)*
**Outcome:**
- Domain patch: `Hold.Reconstruct(...)` static method added — uses `new Hold { ... }` object initializer inside the class (can access `private set` from within the same class)
- 3 document models with full BsonAttributes in `Infrastructure/Persistence/Documents/`
- `MongoTransaction` + `MongoTransactionFactory` in `Infrastructure/Transactions/`
- `CollectionIndexInitializer` — idempotent, `holds` gets 2 compound indexes, `inventory` gets unique `productId` index
- `DatabaseSeeder` — count → 0? insert 5 seed products; `SeedItems` exposed as `static readonly` for reuse
- `MongoInventoryRepository` + `MongoHoldRepository` + `MongoSettingsRepository` — all interfaces implemented
- `Program.cs` wired: `IMongoClient` → `IMongoDatabase` → 3 typed collections → repositories → startup pipeline
- `dotnet test` → **Passed: 31, Failed: 0** (17 Phase 2 + 14 Phase 3), 0 infrastructure needed
- One nullable CS8620 warning on Moq nullability for `FindOneAndUpdateAsync → null` setup — warning only, test passes

---

## Session 4 — 2026-06-28: Phase 4 POST /api/holds

### PROMPT-013
**Phase:** Implementation — Phase 4 Planning
**Tool:** Claude Code plan mode (EnterPlanMode → ExitPlanMode)
**Purpose:** Plan the POST /api/holds endpoint — the most complex in the service. Read progress.md Phase 4 tasks, domain entities/interfaces, HoldSettings, hld.md sequence diagrams, and skills (mongodb-inventory-hold, error-handling-patterns, dotnet-backend-patterns) to derive service flow, retry logic, exception hierarchy, and test structure.
**Prompt Summary:**
> "plan phase 4, check docs folder check AI folder check progress.md and skills and then plan"
**Outcome:** Plan written covering:
- 2 new Domain exceptions: `ProductNotFoundException`, `StockUnavailableException`
- Contracts DTOs: `CreateHoldRequest`, `HoldResponse` (pure records, no domain dependency)
- `HoldService` in `WebApi/Services/` — 8 TDD tests across happy path, validation, stock errors, write conflict retry
- `DomainExceptionHandler` implementing `IExceptionHandler` (.NET 8+ pattern) — maps all domain exceptions to RFC 7807 ProblemDetails
- `HoldEndpoints.cs` — `POST /api/holds` returning 201 Created with Location header
- Key design: `IExceptionHandler` registered via DI + parameterless `UseExceptionHandler()` in .NET 10

---

### PROMPT-014
**Phase:** Implementation — Phase 4 Execution
**Tool:** Claude Code (Write + Edit + PowerShell)
**Purpose:** Execute the approved Phase 4 plan — TDD service layer with full endpoint wiring.
**Prompt Summary:**
> *(Plan approved — implementation proceeded automatically)*
**Outcome:**
- `ProductNotFoundException` + `StockUnavailableException` added to `Domain/Exceptions/`
- `CreateHoldRequest` + `HoldResponse` added to `Contracts/Requests/` and `Contracts/Responses/`
- `CreateHoldServiceTests.cs` — 8 tests written first (RED), then `HoldService.cs` written to pass (GREEN)
- `DomainExceptionHandler` — `IExceptionHandler` implementation mapping 5 domain exception types
- `HoldEndpoints.cs` — `MapPost` with typed `Produces<T>` declarations
- `Program.cs` wired: `AddExceptionHandler<DomainExceptionHandler>()` + `AddScoped<HoldService>()` + `MapHoldEndpoints()`
- `dotnet test` → **Passed: 39, Failed: 0** (31 Phase 2+3 + 8 Phase 4)
- **Non-obvious fix logged:** `when (e.Code == 112)` exception filter silently evaluates to false if `MongoCommandException.Code` property access throws internally (BsonDocument key access). Fixed by checking `e.Message.Contains("WriteConflict")` first — short-circuits before `e.Code` is evaluated. Production behavior unchanged since real write conflicts always have "WriteConflict" in the message.

---

## Session 5 — 2026-06-28: Phase 5 Background Worker

### PROMPT-015
**Phase:** Implementation — Phase 5 Planning
**Tool:** Claude Code plan mode (EnterPlanMode → ExitPlanMode)
**Purpose:** Plan the HoldExpiryWorker background service. Read progress.md Phase 5 tasks, IHoldRepository/IInventoryRepository interfaces, MongoHoldRepository implementation, IInventoryCache and IHoldEventPublisher domain interfaces, and HoldSettings to derive the full worker design including race condition handling.
**Prompt Summary:**
> "plan phase 5"
**Outcome:** Plan written covering:
- Null stubs for `IHoldEventPublisher` and `IInventoryCache` (real implementations in Phases 8/9)
- `HoldExpiryWorker : BackgroundService` with public `ProcessExpiredHoldsAsync` for direct testability
- Race condition handling: `AtomicTransitionAsync` returns null → skip inventory + event
- Cache invalidation only when ≥1 hold successfully transitioned (not on race-lost or empty)
- Delay-first loop pattern to avoid hitting DB before startup seed completes
- 4 TDD tests matching progress.md tasks 5.1–5.4

---

### PROMPT-016
**Phase:** Implementation — Phase 5 Execution
**Tool:** Claude Code (Write + Edit + Bash)
**Purpose:** Execute the approved Phase 5 plan — null stubs, worker, tests, Program.cs wiring.
**Prompt Summary:**
> *(Plan approved — implementation proceeded automatically)*
**Outcome:**
- `NullHoldEventPublisher` + `NullInventoryCache` stubs in `WebApi/Stubs/`
- `HoldExpiryWorker.cs` in `WebApi/Workers/` — `ProcessExpiredHoldsAsync` public, `ExecuteAsync` polls with delay-first
- `HoldExpiryWorkerTests.cs` — 4 tests (NoExpiredHolds, TwoExpiredHolds, RaceCondition, AllRaceLost)
- `Program.cs` wired: stub singletons + `AddHostedService<HoldExpiryWorker>()`
- `dotnet test` → **Passed: 43, Failed: 0** (39 Phase 2+3+4 + 4 Phase 5)

---

## Session 8 — 2026-06-28: Phase 8 RabbitMQ Publisher

### PROMPT-021
**Phase:** Planning — Phase 8
**Tool:** Claude Code plan mode
**Purpose:** Plan RabbitMQ publisher replacing `NullHoldEventPublisher` stub.
**Prompt Summary:**
> "plan phase 8"
**Outcome:** Plan written covering:
- Event DTOs in `Contracts/Events/` (HoldCreatedEvent, HoldReleasedEvent, HoldExpiredEvent, EventItem)
- `RabbitMqConnectionFactory` static helper + `RabbitMqTopologyInitializer` (exchange + 3 queues + bindings)
- `RabbitMqHoldEventPublisher` — one channel per publish, fire-and-forget error handling
- `IHoldEventPublisher` + `ILogger<HoldService>` added to `HoldService` constructor; wired into `CreateHoldAsync` and `ReleaseHoldAsync`
- 4 `RabbitMqPublisherTests` + 2 wiring tests → 66 total

---

### PROMPT-022
**Phase:** Implementation — Phase 8 Execution
**Tool:** Claude Code (Edit + Write + Bash)
**Purpose:** Execute approved Phase 8 plan.
**Prompt Summary:**
> *(Plan approved — implementation proceeded automatically)*
**Outcome:**
- 4 event DTO records created in `Contracts/Events/`
- `RabbitMqHoldEventPublisher`, `RabbitMqConnectionFactory`, `RabbitMqTopologyInitializer` created in `Infrastructure/Messaging/`
- `RabbitMqPublisherTests` (4 tests) using `Mock<IConnection>` + `Mock<IChannel>` — body captured and deserialized to verify JSON fields and routing keys
- `HoldService` constructor updated: `IHoldEventPublisher` + `ILogger<HoldService>` added as last two params; publish wired with try/catch in `CreateHoldAsync` and `ReleaseHoldAsync`
- All 5 `HoldService` test classes updated with `_publisher.Object` + `NullLogger` constructor args
- `Program.cs` updated: `NullHoldEventPublisher` replaced with `RabbitMqHoldEventPublisher`; `IConnection` singleton via `GetAwaiter().GetResult()`; topology init called at startup
- `dotnet test` → **Passed: 66, Failed: 0** (60 Phase 2–7 + 6 Phase 8)

---

## Session 7 — 2026-06-28: Phase 7 Inventory Endpoints

### PROMPT-019
**Phase:** Planning — Phase 7
**Tool:** Claude Code plan mode
**Purpose:** Plan GET /api/inventory and POST /api/inventory/reset endpoints.
**Prompt Summary:**
> "plan phase 7"
**Outcome:** Plan written covering:
- `DeleteAllAsync` missing from `IHoldRepository` — must add to interface + `MongoHoldRepository`
- New `InventoryService` (separate from `HoldService`) with `GetInventoryAsync` (cache-first) and `ResetInventoryAsync` (delete holds → reset inventory → flush cache → return fresh)
- `InventoryItemResponse` contract: productId, name, totalQty, availableQty, heldQty
- 6 TDD tests (3 GetInventory + 3 ResetInventory) → 60 total
- `InventoryEndpoints.cs` with GET /api/inventory and POST /api/inventory/reset

---

### PROMPT-020
**Phase:** Implementation — Phase 7 Execution
**Tool:** Claude Code (Edit + Write + Bash)
**Purpose:** Execute approved Phase 7 plan.
**Prompt Summary:**
> *(Plan approved — implementation proceeded automatically)*
**Outcome:**
- `IHoldRepository.DeleteAllAsync` added to interface; `MongoHoldRepository.DeleteAllAsync` implemented via `DeleteManyAsync(Filter.Empty)`
- `InventoryItemResponse` record created in `Contracts/Responses/`
- `GetInventoryServiceTests` (3) and `ResetInventoryServiceTests` (3) written first (RED)
- `InventoryService` created — `GetInventoryAsync` cache-first, `ResetInventoryAsync` delete → reset → flush → DB fetch
- `InventoryEndpoints.cs` created with GET and POST /reset; `ToResponse()` maps `HeldQuantity` computed property
- `Program.cs` wired: `AddScoped<InventoryService>()` + `app.MapInventoryEndpoints()`
- `dotnet test` → **Passed: 60, Failed: 0** (54 Phase 2–6 + 6 Phase 7)

---

## Session 6 — 2026-06-28: Phase 6 GET & DELETE Endpoints

### PROMPT-017
**Phase:** Implementation — Phase 6 Planning
**Tool:** Claude Code plan mode (EnterPlanMode → ExitPlanMode)
**Purpose:** Plan GET /api/holds/{holdId}, DELETE /api/holds/{holdId}, and GET /api/holds (paginated list). Read progress.md Phase 6 tasks, HoldService, HoldEndpoints, domain interfaces, and HLD API contracts.
**Prompt Summary:**
> "Plan phase 6 properly"
**Outcome:** Plan written covering:
- `PagedResponse<T>` contract in Contracts/Responses/
- `IInventoryCache` added to `HoldService` constructor (breaking change → `CreateHoldServiceTests` fix)
- `GetHoldAsync`: cache-first → DB fallback → set cache
- `ReleaseHoldAsync`: `AtomicTransitionAsync` → null → `GetByIdAsync` → `hold.MarkReleased()` to reuse domain logic for 404/410 distinction
- `ListHoldsAsync`: validate pageSize 1–100, delegate to `GetPagedAsync`
- 11 new TDD tests (3 Get + 4 Release + 4 List) → 54 total
- Private `ToResponse(Hold)` helper extracted to avoid mapping duplication across 4 endpoints

---

### PROMPT-018
**Phase:** Implementation — Phase 6 Execution
**Tool:** Claude Code (Write + Edit + Bash)
**Purpose:** Execute the approved Phase 6 plan.
**Prompt Summary:**
> *(Plan approved — implementation proceeded automatically)*
**Outcome:**
- `PagedResponse<T>` created in `Contracts/Responses/`
- `GetHoldServiceTests` (3), `ReleaseHoldServiceTests` (4), `ListHoldsServiceTests` (4) written first (RED)
- `CreateHoldServiceTests` updated: added `Mock<IInventoryCache>` field + updated constructor call
- `HoldService` updated: `IInventoryCache cache` added to constructor; `GetHoldAsync`, `ReleaseHoldAsync`, `ListHoldsAsync` implemented
- `HoldEndpoints` refactored: extracted `ToResponse()` helper; added GET /{holdId}, DELETE /{holdId}, GET / endpoints
- **Compile fix:** C# requires optional parameters after required ones — reordered `MapGet("/")` lambda to put `HoldService service, CancellationToken ct` before `int page = 1, int pageSize = 20`
- `dotnet test` → **Passed: 54, Failed: 0** (43 Phase 2–5 + 11 Phase 6)

*(Additional prompts will be logged here as the session progresses)*

---

### PROMPT-023
**Phase:** Verification — Phase 8 Manual Testing
**Tool:** Claude Code (Read + Edit — diagnostic logging)
**Purpose:** Diagnose why RabbitMQ exchange and queues were not appearing in the Management UI after Phase 8 implementation.
**Prompt Summary:**
> "I am not able to see rabbitmq queue in management ui" / "restarted api no channel, no exchange no queue"
**Outcome:**
- Root cause identified: first `dotnet run` used old binary before the diagnostic logging edit was compiled in
- Added `ILogger<RabbitMqTopologyInitializer>` to `RabbitMqTopologyInitializer` with per-step log messages and a try/catch that re-throws — now logs endpoint, channel open, exchange declare, each queue declare/bind, and completion
- Second `dotnet run` confirmed: `amqp://localhost:5672`, exchange declared, 3 queues declared and bound — topology init complete
- Key insight documented: `appsettings.json` uses `"Host": "rabbitmq"` (Docker service name for container-to-container), `appsettings.Development.json` overrides to `"Host": "localhost"` for local dev — this layering is intentional and correct
- Phase 8 manual verification complete: messages confirmed in `hold.created.queue` and `hold.expired.queue`
