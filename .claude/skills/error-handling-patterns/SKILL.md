---
name: error-handling-patterns
description: "Build resilient applications with robust error handling strategies that gracefully handle failures and provide excellent debugging experiences."
risk: safe
source: community
date_added: "2026-02-27"
---

# Error Handling Patterns

Build resilient applications with robust error handling strategies that gracefully handle failures and provide excellent debugging experiences.

## Use this skill when

- Implementing error handling in new features
- Designing error-resilient APIs
- Debugging production issues
- Improving application reliability
- Creating better error messages for users and developers
- Implementing retry and circuit breaker patterns
- Handling async/concurrent errors
- Building fault-tolerant distributed systems

## Do not use this skill when

- The task is unrelated to error handling patterns
- You need a different domain or tool outside this scope

## Key Patterns

### RFC 7807 ProblemDetails (REST APIs)
```json
{
  "type": "https://example.com/problems/insufficient-stock",
  "title": "Insufficient stock",
  "status": 409,
  "detail": "Product widget-a has only 3 units available, but 5 were requested.",
  "instance": "/api/holds"
}
```

### Retry with Exponential Backoff
- Catch specific transient exceptions (e.g., MongoCommandException code 112)
- Retry up to N times with exponential backoff (e.g., 50ms, 100ms, 200ms)
- After exhaustion, return appropriate HTTP status (409, 503)

### Circuit Breaker
- Track failure rate per dependency
- Open circuit after threshold to prevent cascade failures
- Half-open state to test recovery

### Result Pattern (C#)
- Use `Result<T>` or `OneOf<Success, Error>` instead of exceptions for expected failures
- Reserve exceptions for truly exceptional/unexpected conditions

### Global Exception Handler (ASP.NET Core)
- Middleware to catch unhandled exceptions
- Map to ProblemDetails with appropriate status code
- Log full exception details server-side; return sanitized message to client

## Instructions

- Clarify goals, constraints, and required inputs.
- Apply relevant best practices and validate outcomes.
- Provide actionable steps and verification.
- If detailed examples are required, open `resources/implementation-playbook.md`.

## Resources

- `resources/implementation-playbook.md` for detailed patterns and examples.

## Limitations
- Use this skill only when the task clearly matches the scope described above.
- Do not treat the output as a substitute for environment-specific validation, testing, or expert review.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
