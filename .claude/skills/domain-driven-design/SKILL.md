---
name: domain-driven-design
description: "Guide practitioners through strategic modeling, bounded contexts, and tactical implementation of Domain-Driven Design patterns."
risk: safe
source: community
date_added: "2026-02-27"
---

# Domain-Driven Design

Guide teams through strategic modeling, bounded contexts, and tactical implementation of Domain-Driven Design patterns.

## Use this skill when

- Modeling complex business domains with explicit boundaries
- Deciding whether full DDD warrants the implementation overhead (use viability criteria)
- Starting a new service and needing domain analysis before coding
- Routing to the right specialized DDD sub-skill

## Do not use this skill when

- The domain is simple CRUD — use standard REST patterns instead
- You're already deep in tactical implementation (use `@ddd-tactical-patterns` directly)
- The boundaries are stable and already partitioned

## Viability Criteria

Recommend full DDD when ≥2 conditions are true:
- Business rules are complex or fast-changing
- Multiple teams cause model collisions
- Integration contracts remain unstable
- Auditability and explicit invariants are critical

## Routing

This skill routes work to specialized agents:

- Strategic artifacts → `@ddd-strategic-design`
- Cross-context translation → `@ddd-context-mapping`
- Implementation patterns → `@ddd-tactical-patterns`
- Read/write separation → `@cqrs-implementation`
- Event-driven architecture → `@event-sourcing-architect`, `@event-store-design`

## Required Outputs

Every engagement must deliver: scope definition, current stage identification, explicit artifacts, and risk assessment with next steps.

## Limitations
- Does not replace domain expert workshops.
- Does not provide framework-specific code generation.
- Use this skill only when the task clearly matches the scope described above.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
