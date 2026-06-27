# Design Discussion
## Inventory Hold Microservice — Requirements Clarification Q&A

**Brainstorm Duration:** 1 hour 15 minutes

This document captures the full architectural Q&A session conducted before writing any code. Each question surfaces an ambiguity in the assignment spec, presents trade-offs, and records the decision made. The goal was to resolve all unknowns upfront so implementation has no guesswork.

---

### Q1: Hold Cardinality — Single product or multi-product per hold?

**Question:** Can a single hold contain multiple products/items (e.g., a cart-level hold), or is each hold scoped to a single product + quantity? The frontend shows "select products" (plural) suggesting multi-item, but the POST /api/holds spec is ambiguous.

**Answer:** Multi-item hold — a single hold covers multiple products (cart-level). A customer checking out with 2x Widget A + 1x Widget B produces **one hold document** containing both line items.

**Architectural implication:** Atomic deduction across multiple inventory documents requires MongoDB multi-document transactions (session-based `withTransaction`), not a simple single-document `findOneAndUpdate`.

---

### Q2: Active Holds List — Missing List Endpoint

**Question:** The spec defines 4 endpoints but the frontend requires an "Active Holds List." How should this be populated — is there an implied `GET /api/holds` list endpoint?

**Answer:** Yes — add `GET /api/holds` as a 5th endpoint returning the complete list of holds. Fragile client-side ID tracking is not acceptable.

**Architectural implication:** Add `GET /api/holds` with optional `?status=active` filter. This endpoint will need Redis caching consideration — hold lists change frequently (on every create/release/expire), so TTL must be short or cache must be invalidated on mutations.

---

### Q3: Hold Expiry Mechanism — Proactive vs Lazy

**Question:** The spec says "HoldExpired — when a hold is detected as expired." Should this be a lazy on-read check or a proactive background worker?

**Answer:** Background worker (`IHostedService`) with **30-second polling interval (configurable)**. Hold expires exactly at `expiresAt`. Inventory document stores `availableQuantity` as a materialized pre-computed value — background worker atomically restores it when holds expire. `GET /api/inventory` reads documents directly, no hold joins needed.

**Architectural implication:**
- `availableQuantity` is the single source of truth on inventory documents
- `POST /api/holds` → `$inc availableQuantity -N` (atomic)
- Background worker expiry → `$inc availableQuantity +N` + marks hold `Expired` + publishes `HoldExpired` event
- `DELETE /api/holds` (release) → `$inc availableQuantity +N` + marks hold `Released` + publishes `HoldReleased` event
- `GET /api/inventory` → pure read, Redis-cacheable aggressively

---

### Q4: DELETE on Expired Hold — HTTP Response

**Question:** If background worker already expired the hold and restored inventory, then client calls `DELETE /api/holds/{holdId}` — inventory must NOT be restored again (double-restoration bug). What HTTP response?

**Answer:** `410 Gone` — hold existed but is permanently gone (terminal state). Inventory is not touched. Frontend should treat 410 as a terminal state and remove the hold from the UI.

**Architectural implication:** Hold status must be checked BEFORE any inventory mutation on DELETE. Only restore inventory if hold status is `Active`. If `Expired` or `Released` → return appropriate code without touching inventory.

---

### Q5: GET /api/holds/{holdId} for Expired Hold — HTTP Response

**Question:** What should GET return for an expired hold — 200 with status, 410, or 404?

**Answer:** `200 OK` with `status: "Expired"` (full hold document including items, expiresAt). DELETE uses 410 for terminal states, GET always returns the document with semantic status. This is the industry-standard REST pattern (Stripe, Shopify).

**Rule:** HTTP error codes on mutations that can't complete. HTTP 200 with semantic status on reads of resources in terminal states.

**Hold status enum:** `Active` | `Released` | `Expired`

---

### Q6: Hold Expiration Duration — Config Scope

**Question:** Is expiration duration a config-file value or can clients override it per-request?

**Answer:** Config-only (no per-request override). BUT store the config in MongoDB (a settings/config collection) so it can be toggled at runtime without redeployment. Fall back to `appsettings.json` default (15 minutes) if DB config is not found.

**Pattern:**
```
MongoDB: settings collection → { key: "HoldExpirationMinutes", value: 15 }
appsettings.json → "HoldSettings": { "ExpirationMinutes": 15 }  ← fallback
```
Service reads from DB first on each hold creation, falls back to appsettings if absent.

**Architectural implication:** Add a `SettingsRepository` and cache the expiration value in Redis (short TTL ~60s) to avoid a DB read on every hold creation.

---

### Q7: Customer Identity on a Hold

**Question:** Should holds be anonymous or carry a customer identifier?

**Answer:** Optional `customerName` string field on `POST /api/holds` request body. Not authenticated or verified — client passes any string (e.g., "John", "guest-abc"). Stored on the hold document for display in the Active Holds List.

**Architectural implication:** Hold document gets a nullable `customerName` field. Frontend "Create Hold" form adds an optional name input. Active Holds List shows "John's hold — 2x Widget A" for better UX.

---

### Q8: Partial Hold Failure Behavior

**Question:** If one item in a multi-item hold has insufficient stock, does the entire hold fail or do we partial-hold the available items?

**Answer:** All-or-nothing (atomic rollback). Return `409 Conflict` with a descriptive payload listing exactly which items failed and why (productId, requested qty, available qty). Frontend shows user-friendly message: "Widget C is unavailable — remove it to continue checkout."

**Architectural implication:** MongoDB transaction wraps the entire hold creation. Phase 1: validate ALL items have sufficient `availableQuantity`. Phase 2: only if ALL pass, execute all `$inc` operations and create hold document. On any failure in Phase 1: abort transaction, return 409 with failure details.

---

### Q9: GET /api/holds — Filtering and Pagination

**Question:** Does the holds list endpoint support server-side filtering and pagination?

**Answer:** Yes to both.
- **Filter:** `?status=active|released|expired` (server-side). Frontend has a toggle — ON shows only `Active` holds, OFF shows all statuses (Active + Released + Expired).
- **Pagination:** `?page=1&pageSize=20` — standard page/pageSize pattern.
- Default: `status=active`, `page=1`, `pageSize=20` when params are omitted.

**Response shape:**
```json
{
  "data": [...],
  "total": 45,
  "page": 1,
  "pageSize": 20,
  "totalPages": 3
}
```

**Architectural implication:** MongoDB query with `{ status: filter }` + `.Skip().Limit()`. Cache strategy for paginated lists needs careful TTL — short TTL (10-15s) or no cache (mutations are frequent). Count query needed for totalPages.

---

### Q10: GET /api/inventory — Response Shape

**Question:** Should inventory expose availableQuantity only, or the full breakdown (total, available, held)?

**Answer:** Full breakdown — `totalQuantity`, `availableQuantity`, and computed `heldQuantity = totalQuantity - availableQuantity`. Makes the dashboard demonstrate hold effects visually — reviewer can see stock move from available to held in real time.

**Architectural implication:** `heldQuantity` is computed on read (not stored), derived from `totalQuantity - availableQuantity`. No extra DB query needed.

---

### Q11: RabbitMQ Exchange & Queue Topology

**Question:** What exchange type and queue structure for the 3 hold lifecycle events?

**Answer:** Direct Exchange with one queue per event type.
```
Exchange: inventory.hold.events (direct)
  → routing key: hold.created  → Queue: hold.created.queue
  → routing key: hold.released → Queue: hold.released.queue
  → routing key: hold.expired  → Queue: hold.expired.queue
```
Downstream consumers bind only to the event types they care about. Most production-realistic pattern for a multi-consumer commerce platform.

---

### Q12: API Error Response Format

**Question:** RFC 7807 ProblemDetails or custom error envelope?

**Answer:** RFC 7807 ProblemDetails throughout. Domain-specific data (e.g., insufficient stock failures list) goes in ProblemDetails extension fields. Aligns with .NET 10 defaults (`ValidationProblem()`, `UseExceptionHandler()`).

---

### Q13: Redis Caching Strategy

**Question:** Which endpoints get cached and with what TTLs?

**Answer:**
| Endpoint | Cached | TTL | Invalidation |
|---|---|---|---|
| `GET /api/inventory` | Yes | 30s | On any hold mutation |
| `GET /api/holds/{holdId}` | Yes | 60s | On hold state change |
| `GET /api/holds` (list) | **No** | — | — |
| Settings (expiration config) | Yes | 60s | On settings change |

**Reason for not caching holds list:** The list is too dynamic — it changes every 30s from the background worker plus every create/release mutation. Cache invalidation on every write would add additional load on the system, negating any read benefit. Direct MongoDB reads with proper indexing (`status`, `createdAt`) is the correct approach for this endpoint.

**Cache key pattern:**
```
inventory:all           → GET /api/inventory
hold:{holdId}           → GET /api/holds/{holdId}
settings:expiration     → hold expiration minutes
```
**Strategy:** Write-through invalidation — delete relevant keys on mutation, let next read repopulate.

---

### Q14: Frontend State Management

**Question:** Which state management library — TanStack Query, Zustand, or Redux?

**Answer:** Both TanStack Query + Zustand together.
- **TanStack Query** → all server state (inventory levels, holds list, hold detail). Handles fetch, cache, background refetch, and `invalidateQueries` after mutations — directly solves the "stay in sync without page refresh" requirement.
- **Zustand** → UI state only (filter toggle "active only", pagination page, confirmation modal state).

**Why not Redux:** Overkill for this scope. **Why not Zustand alone:** Not designed for server state — you'd reimplement TanStack Query manually.

---

### Q15: DELETE on Already-Released Hold

**Question:** What HTTP response for DELETE on a hold that was already successfully released?

**Answer:** `410 Gone` — consistent with expired holds. Both Released and Expired are terminal states. ProblemDetails `detail` field distinguishes them: `"This hold was already released at {timestamp}."` vs `"This hold expired at {timestamp}."` Client logic stays simple: 410 always means "terminal state, stop retrying."

---

### Q16: RabbitMQ Publish Failure Handling

**Question:** If RabbitMQ publish fails — fire-and-forget, fail the operation, or outbox pattern?

**Answer:** Fire-and-forget (Option A) — log the failure, continue. Hold DB operation is already committed and is not rolled back.

**Why not outbox:** Over-engineering for 2-day assignment scope. **Why not fail the operation:** Couples checkout UX to RabbitMQ availability — a transient broker blip kills checkout for the customer.

**Documented trade-off:** Production-grade approach would be the Transactional Outbox Pattern — persist event to MongoDB outbox collection inside the same transaction, separate worker publishes and marks as sent. Guarantees at-least-once delivery without coupling HTTP response latency to broker availability.

---

### Q17: "Time Remaining" Countdown — Server vs Client

**Question:** Should "time remaining" be server-computed or client-computed?

**Answer:** Client-computed from `expiresAt` ISO timestamp in the API response. Frontend runs a `setInterval` countdown locally. When timer hits 0, TanStack Query refetches to confirm expiry (background worker may not have run yet within 30s window) and transitions the hold status display.

**Why not server-computed remainingSeconds:** Stales immediately after the response is received. Requires constant polling to stay accurate.

---

### Q18: Swagger / OpenAPI Documentation

**Question:** Should the API expose Swagger UI for reviewers to test endpoints?

**Answer:** Yes. .NET 10 native OpenAPI support (`Microsoft.AspNetCore.OpenApi`) — no Swashbuckle needed. Swagger UI at `/swagger`. Lets reviewers test all 5 endpoints directly in the browser without curl or Postman setup.

**Cost:** ~5 lines in `Program.cs`. Zero extra packages in .NET 10.

---

### Q19: Frontend Deployment — Docker vs Separate

**Question:** Should the frontend be included in docker-compose or run separately via npm run dev?

**Answer:** Included in docker-compose. Multi-stage Dockerfile: Node build stage (Vite) → Nginx serve stage. One command (`docker-compose up --build`) starts the entire stack including frontend. Matches the submission requirement of "working one-command startup."

**Implementation:** Nginx reverse-proxies `/api/*` calls to the .NET API container — no CORS issues, no hardcoded ports in the frontend.

---

### Q20: Input Validation — Hold Request Edge Cases

**Question:** How should the API handle invalid hold request inputs (empty items, zero quantity, duplicate productIds, non-existent productId)?

**Answer:**
| Scenario | Response |
|---|---|
| Empty `items` array | `422 Unprocessable Entity` |
| `quantity <= 0` | `422 Unprocessable Entity` |
| Duplicate `productId` in same request | Prevented in frontend (product removed from dropdown once selected). Backend adds defensive `422` check as last-resort guard. |
| Non-existent `productId` | `404 Not Found` with ProblemDetails — product doesn't exist in inventory |

**Frontend approach for duplicates:** Once a product is selected in the Create Hold form, it is removed from the available product dropdown. User can only adjust quantity on already-selected items, not add the same product twice.

---

### Q21: Health Check Endpoint

**Question:** Should the API expose `/health` with dependency checks for MongoDB, Redis, and RabbitMQ?

**Answer:** Yes. .NET 10 built-in health check middleware. Used by docker-compose `depends_on: condition: service_healthy` to ensure the API only starts after all infrastructure is ready — prevents startup race conditions. Reports per-dependency status: MongoDB, Redis, RabbitMQ.

**Architectural implication:** Eliminates the most common docker-compose startup failure (API connecting before MongoDB is initialized).

---

### Q22: Database Seeding Strategy

**Question:** Should seeding happen every startup (always reset) or only when empty?

**Answer:** Seed only when inventory collection is empty on startup, plus expose `POST /api/inventory/reset` endpoint to restore initial state without restarting Docker.

**Behavior:**
- First `docker-compose up --build` → seeds 5 products
- Subsequent restarts → skips seeding (preserves demo state)
- Reviewer calls `POST /api/inventory/reset` → wipes all holds, restores inventory to initial quantities

**Architectural implication:** Reset endpoint deletes all hold documents, sets all inventory `availableQuantity` back to `totalQuantity`, and invalidates all Redis cache keys.

---

### Q23: Background Worker vs Client DELETE Race Condition

**Question:** If the background worker and a client DELETE call both try to expire/release the same hold simultaneously, how do we prevent double inventory restoration?

**Answer:** Atomic `findOneAndUpdate` with a status guard filter on BOTH paths:
```
filter: { _id: holdId, status: "Active" }
update: { $set: { status: "Expired|Released" } }
```
MongoDB guarantees only ONE operation matches the filter. The other gets `null` back — knows it lost the race, skips inventory restoration and event publishing entirely. No transaction needed — single-document atomic update is sufficient for this case.

**Result:** Zero possibility of double inventory restoration regardless of timing. No distributed lock needed.

---

## Decisions Summary

| # | Decision | Choice |
|---|---|---|
| Q1 | Hold cardinality | Multi-item (cart-level hold) |
| Q2 | Hold list endpoint | `GET /api/holds` added as 5th endpoint |
| Q3 | Expiry mechanism | Background worker, 30s polling (configurable), materialized `availableQuantity` |
| Q4 | DELETE on expired hold | `410 Gone` |
| Q5 | GET on expired hold | `200 OK` with `status: "Expired"` |
| Q6 | Expiration config | Config-only from MongoDB settings collection, appsettings fallback |
| Q7 | Customer identity | Optional `customerName` string on request |
| Q8 | Partial hold failure | All-or-nothing, `409` with per-item failure details |
| Q9 | Holds list filtering | Server-side `?status=` filter + pagination (`?page=&pageSize=`) |
| Q10 | Inventory response | Full breakdown: `totalQuantity`, `availableQuantity`, `heldQuantity` |
| Q11 | RabbitMQ topology | Direct exchange, 3 queues (one per event type) |
| Q12 | Error format | RFC 7807 ProblemDetails throughout |
| Q13 | Redis caching | Cache inventory + single hold. No cache on holds list (too dynamic) |
| Q14 | Frontend state | TanStack Query (server state) + Zustand (UI state) |
| Q15 | DELETE on released hold | `410 Gone` |
| Q16 | RabbitMQ publish failure | Fire-and-forget with logging |
| Q17 | Time remaining | Client-computed from `expiresAt` timestamp |
| Q18 | Swagger UI | Yes — `/swagger`, .NET 10 native OpenAPI |
| Q19 | Frontend deployment | In docker-compose (Nginx multi-stage Dockerfile) |
| Q20 | Input validation | `422` for empty/invalid, frontend prevents duplicate productIds |
| Q21 | Health checks | Yes — `/health` with MongoDB + Redis + RabbitMQ checks |
| Q22 | DB seeding | Seed on empty + `POST /api/inventory/reset` endpoint |
| Q23 | Worker/DELETE race | Atomic `findOneAndUpdate` with `status: "Active"` guard |
