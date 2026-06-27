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

*(Additional prompts will be logged here as the session progresses)*
