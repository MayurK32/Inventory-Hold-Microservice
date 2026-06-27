---
name: ddd-context-mapping
description: "Establish integration patterns between DDD bounded contexts. Define anti-corruption layers, shared kernels, and upstream/downstream relationships."
risk: safe
source: community
date_added: "2026-02-27"
---

# DDD Context Mapping

Establish integration patterns between Domain-Driven Design bounded contexts. Define ownership, translation layers, and dependency direction across service boundaries.

## Use this skill when

- Defining integration patterns between bounded contexts
- Preventing domain leakage across service boundaries
- Planning anti-corruption layers during system migrations
- Clarifying ownership responsibilities for service contracts
- Mapping relationships and dependency directions between context pairs

## Do not use this skill when

- Working within a single bounded context (use `@ddd-tactical-patterns`)
- Building a monolith without explicit context separation
- Selecting infrastructure tooling

## Core Process

1. Enumerate all context pairs with their dependency flow
2. Select appropriate relationship pattern for each pair
3. Establish translation rules and boundaries
4. Document failure modes and versioning strategies

## Relationship Patterns

| Pattern | Description |
|---------|-------------|
| **Shared Kernel** | Small shared model; both teams maintain it |
| **Customer/Supplier** | Upstream supplies API; downstream consumes |
| **Conformist** | Downstream conforms to upstream model (no ACL) |
| **Anti-Corruption Layer (ACL)** | Translate upstream model to local domain model |
| **Open Host Service** | Well-defined protocol for multiple consumers |
| **Published Language** | Shared event schema (e.g., RabbitMQ events) |

## Required Outputs

- Context relationship map (which depends on which)
- Contract ownership matrix (who owns the API/event schema)
- Translation decisions (where ACLs are needed)
- Documented coupling risks with mitigation

## Limitations
- Does not handle single-context systems or internal class design.
- Does not replace API-level schema design.
- Cannot independently guarantee organizational alignment.
- Use this skill only when the task clearly matches the scope described above.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
