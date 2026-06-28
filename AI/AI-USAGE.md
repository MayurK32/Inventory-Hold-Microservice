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

## Human Audit
*(Specific examples of AI suggestions accepted and rejected — to be documented during development)*

---

## Verification
*(How AI was used to generate tests and how AI-generated code was validated — to be documented during development)*
