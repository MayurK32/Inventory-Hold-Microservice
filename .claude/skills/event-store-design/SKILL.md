---
name: event-store-design
description: "Design and implement event stores for event-sourced systems. Use when building event sourcing infrastructure, choosing event store technologies, or implementing event persistence patterns."
risk: unknown
source: community
date_added: "2026-02-27"
---

# Event Store Design

Comprehensive guide to designing event stores for event-sourced applications.

## Use this skill when

- Designing event sourcing infrastructure
- Choosing between event store technologies
- Implementing custom event stores
- Optimizing event storage and retrieval
- Setting up event store schemas
- Planning for event store scaling

## Do not use this skill when

- The task is unrelated to event store design
- You need a different domain or tool outside this scope

## Core Concepts

### Event Store Requirements

| Requirement | Description |
|-------------|-------------|
| **Append-only** | Events are immutable, only appends |
| **Ordered** | Per-stream and global ordering |
| **Versioned** | Optimistic concurrency control |
| **Subscriptions** | Real-time event notifications |
| **Idempotent** | Handle duplicate writes safely |

## Technology Comparison

| Technology | Best For | Limitations |
|------------|----------|-------------|
| **EventStoreDB** | Pure event sourcing | Single-purpose |
| **PostgreSQL** | Existing Postgres stack | Manual implementation |
| **Kafka** | High-throughput streaming | Not ideal for per-stream queries |
| **DynamoDB** | Serverless, AWS-native | Query limitations |
| **Marten** | .NET ecosystems | .NET specific |

## Key Schema Pattern (PostgreSQL)

```sql
CREATE TABLE events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    stream_id VARCHAR(255) NOT NULL,
    stream_type VARCHAR(255) NOT NULL,
    event_type VARCHAR(255) NOT NULL,
    event_data JSONB NOT NULL,
    metadata JSONB DEFAULT '{}',
    version BIGINT NOT NULL,
    global_position BIGSERIAL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    CONSTRAINT unique_stream_version UNIQUE (stream_id, version)
);

CREATE INDEX idx_events_stream_id ON events(stream_id, version);
CREATE INDEX idx_events_global_position ON events(global_position);
```

## Best Practices

- **Use stream IDs that include aggregate type** — `Order-{uuid}`
- **Include correlation/causation IDs** — for tracing
- **Version events from day one** — plan for schema evolution
- **Implement idempotency** — use event IDs for deduplication
- **Don't update or delete events** — they're immutable facts

## Instructions

- Clarify goals, constraints, and required inputs.
- Apply relevant best practices and validate outcomes.
- Provide actionable steps and verification.
- If detailed examples are required, open `resources/implementation-playbook.md`.

## Limitations
- Use this skill only when the task clearly matches the scope described above.
- Do not treat the output as a substitute for environment-specific validation, testing, or expert review.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
