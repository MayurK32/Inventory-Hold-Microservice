---
name: api-testing-observability-api-mock
description: "Create realistic mock API services for frontend testing, simulating third-party services, and validating API contracts before backend implementation is complete."
risk: safe
source: community
date_added: "2026-02-27"
---

# API Mock — Testing & Observability

Create realistic mock API services for frontend testing, simulating third-party services, and validating API contracts before backend work is complete.

## Use this skill when

- Building mock APIs for frontend testing
- Simulating third-party services during development
- Creating demo environments without a live backend
- Validating API contracts before backend implementation
- Testing error scenarios and edge cases

## Do not use this skill when

- Testing production systems
- Security or penetration testing
- You lack an API contract to reference

## Key Approach

1. **Contract clarification** — clarify the API contract, auth flows, error shapes, and latency expectations first
2. **Plan before coding** — define routes, scenarios, and state transitions
3. **Realistic fixtures** — deterministic test data with optional randomness
4. **Clear documentation** — instructions for running the mock and switching scenarios

## Safety Guidelines

- Never reuse production secrets or real customer data in mocks
- Clearly label mock endpoints to prevent accidental production use
- Mock endpoints should return data matching the real API contract exactly

## Resources

- `resources/implementation-playbook.md` for code samples, checklists, and templates.

## Limitations
- Scoped specifically to mock API creation — not a substitute for real integration testing.
- Not for security testing or production system testing.
- Use this skill only when the task clearly matches the scope described above.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
