---
name: ddd-tactical-patterns
description: "Implement Domain-Driven Design tactical patterns: entities, value objects, aggregates, repositories, and domain events."
risk: safe
source: community
date_added: "2026-02-27"
---

# DDD Tactical Patterns

Implement Domain-Driven Design tactical patterns — entities, value objects, aggregates, repositories, and domain events — to build behavior-rich domain models.

## Use this skill when

- Translating domain rules into code structures
- Refactoring an anemic model into behavior-rich domain objects
- Designing aggregates that enforce business invariants
- Defining repository contracts at aggregate root boundaries
- Publishing domain events for significant state changes

## Do not use this skill when

- Still working on strategic boundaries (use `@ddd-strategic-design` first)
- Handling purely presentational or infrastructure concerns
- The domain is simple CRUD without complex rules

## Core Principles

1. **Start with invariants** — design aggregates around what must always be true
2. **Immutable value objects** — use for validated concepts (Money, Quantity, ProductId)
3. **Behavior in domain objects** — not in services or infrastructure
4. **Domain events** — publish for significant state changes (HoldCreated, HoldExpired)
5. **Repository contracts** — defined at aggregate root boundaries, implemented in infrastructure

## Key Patterns

### Aggregates
- Enforce all business rules within the aggregate boundary
- Only expose behavior via methods, not property setters
- Return domain events from commands

### Value Objects
- Immutable; equality by value not identity
- Self-validating on construction
- Use records in C#

### Domain Events
- Named in past tense (HoldCreated, InventoryReserved)
- Contain enough data for downstream handlers
- Published after state change commits

### Repositories
- Interface defined in Domain layer
- Implementation in Infrastructure layer
- One repository per aggregate root

## Example (C#)

```csharp
public class Hold : AggregateRoot
{
    private Hold() { }

    public static Hold Create(string? customerName, IReadOnlyList<HoldItem> items, DateTime expiresAt)
    {
        if (!items.Any()) throw new DomainException("Hold must have at least one item.");
        var hold = new Hold
        {
            Id = Guid.NewGuid().ToString(),
            CustomerName = customerName,
            Items = items,
            Status = HoldStatus.Active,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };
        hold.AddDomainEvent(new HoldCreatedEvent(hold));
        return hold;
    }
}
```

## Limitations
- Focuses exclusively on tactical implementation — not deployment architecture or technology selection.
- Use this skill only when the task clearly matches the scope described above.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
