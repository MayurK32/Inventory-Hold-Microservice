---
name: architecture-decision-records
description: "Create and maintain Architecture Decision Records (ADRs) that capture the rationale, context, and consequences of significant technical decisions."
risk: safe
source: community
date_added: "2026-02-27"
---

# Architecture Decision Records

Create and maintain Architecture Decision Records (ADRs) that capture the rationale, context, and consequences of significant technical decisions.

## Use this skill when

- Recording significant architectural choices (framework, database, API style)
- Establishing institutional memory for technology decisions
- Onboarding new team members to past decisions
- Superseding a previously accepted decision

## Do not use this skill when

- The decision is minor (version bumps, bug fixes, routine maintenance)
- The choice is reversible with low cost

## ADR Templates

### Standard (MADR)
```markdown
# ADR-NNN: Title

## Status
Accepted

## Context
What is the issue motivating this decision?

## Decision
What was decided?

## Consequences
What are the resulting trade-offs?
```

### Lightweight
```markdown
# ADR-NNN: Title
Date: YYYY-MM-DD
Status: Accepted

## Decision
One paragraph.

## Trade-offs
- Pro: ...
- Con: ...
```

### Y-Statement
```
In the context of [situation],
facing [concern],
we decided [option],
to achieve [quality],
accepting [downside].
```

## Status Values
- **Proposed** — under review
- **Accepted** — approved and active
- **Deprecated** — still in use but being phased out
- **Superseded** — replaced by a newer ADR (link to it)
- **Rejected** — evaluated and not adopted

## Critical Practices

- Document trade-offs honestly — include real drawbacks
- Link related ADRs bidirectionally
- Never modify accepted ADRs; create superseding records instead
- Organize in `/docs/adr/` with sequential numbering (ADR-001, ADR-002…)
- Maintain an index file tracking all ADRs and their status

## Limitations
- Use this skill only when the task clearly matches the scope described above.
- Do not treat the output as a substitute for environment-specific validation, testing, or expert review.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
