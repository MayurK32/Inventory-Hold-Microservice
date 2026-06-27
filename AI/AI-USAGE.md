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

---

## Design Discussion

**Brainstorm Duration:** 1 hour 15 minutes

Before writing a single line of code, a structured Q&A session was conducted to surface and resolve every ambiguity in the assignment spec. 23 questions were asked and answered, covering concurrency model, expiry mechanism, API contracts, caching strategy, RabbitMQ topology, frontend architecture, and edge cases.

Full Q&A session and decisions summary: **[design-discussion.md](./design-discussion.md)**

The file contains:
- 23 questions with the full trade-off analysis presented for each
- The decision made and the reasoning behind it
- Architectural implications of each decision
- A final decisions summary table for quick reference

---

## Human Audit
*(Specific examples of AI suggestions accepted and rejected — to be documented during development)*

---

## Verification
*(How AI was used to generate tests and how AI-generated code was validated — to be documented during development)*
