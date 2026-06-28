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

## Human Audit
*(Specific examples of AI suggestions accepted and rejected — to be documented during development)*

---

## Verification
*(How AI was used to generate tests and how AI-generated code was validated — to be documented during development)*
