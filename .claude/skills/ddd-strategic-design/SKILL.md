---
name: ddd-strategic-design
description: "Design DDD strategic artifacts including subdomains, bounded contexts, and ubiquitous language for complex business domains."
risk: safe
source: community
date_added: "2026-02-27"
---

# DDD Strategic Design

Design Domain-Driven Design strategic artifacts: subdomain classification, bounded context inventory, ubiquitous language, and team alignment.

## Use this skill when

- Categorizing subdomains (core, supporting, generic)
- Establishing domain boundaries across distributed systems
- Aligning organizational teams with bounded contexts
- Creating shared terminology with business stakeholders
- Starting a new service or system decomposition

## Do not use this skill when

- The domain is already stable and partitioned
- You need purely tactical implementation patterns (use `@ddd-tactical-patterns`)
- The work is infrastructure-focused

## Required Outputs

Every engagement must produce:
1. **Subdomain classification table** — core / supporting / generic for each domain area
2. **Bounded context inventory** — name, owner team, responsibilities, language
3. **Terminology glossary** — canonical terms per bounded context
4. **Boundary rationale** — why each line was drawn where it was

## Key Concepts

### Subdomain Types
- **Core domain** — competitive differentiator; invest heavily (e.g., Inventory Hold logic)
- **Supporting domain** — necessary but not differentiating (e.g., Notifications)
- **Generic domain** — commodity; buy/use off-the-shelf (e.g., Auth, Email)

### Bounded Context
- Explicit boundary around a domain model
- One ubiquitous language per context
- Team ownership aligns to context

### Ubiquitous Language
- Terms agreed between developers and domain experts
- Used consistently in code, docs, conversations
- Different contexts may use different meanings for the same word

## Limitations
- Requires genuine stakeholder collaboration — cannot substitute for domain expert workshops.
- Does not cover tactical design patterns that follow strategic decisions.
- Use this skill only when the task clearly matches the scope described above.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
