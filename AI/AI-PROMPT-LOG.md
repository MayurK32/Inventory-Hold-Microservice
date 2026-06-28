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

*(Additional prompts will be logged here as the session progresses)*
