---
name: api-patterns
description: "Learn to THINK, not copy fixed patterns. Contextual decision-making for REST, GraphQL, and tRPC API architecture."
risk: safe
source: community
date_added: "2026-02-27"
---

# API Patterns

Learn to THINK, not copy fixed patterns. Contextual decision-making framework for REST, GraphQL, and tRPC API architecture.

## Use this skill when

- Making architectural decisions about API style and structure
- Choosing between REST, GraphQL, and tRPC
- Designing consistent response formats and error schemas
- Planning versioning, authentication, and rate limiting

## Do not use this skill when

- You already know which API style to use and need implementation help
- You need infrastructure or database design

## Core Philosophy

**"Learn to THINK, not copy fixed patterns."**

Every API decision should be contextual — driven by consumer needs, not convention.

## Decision Framework

Before designing an API, answer:
1. **Who are the consumers?** (browser, mobile, third-party, internal service)
2. **What style fits?** REST for CRUD resources, GraphQL for flexible queries, tRPC for type-safe TS monorepos
3. **What's the response format?** Use RFC 7807 ProblemDetails for errors; consistent success envelope
4. **What versioning strategy?** URI path (`/v1/`), header, or content negotiation
5. **How is auth handled?** JWT, API key, OAuth 2.0 — choose per consumer type
6. **Rate limiting?** Per-user and per-IP; stricter on auth endpoints

## Common Pitfalls

- `/getUsers` — verbs in URLs (use `GET /users`)
- Inconsistent response structures across endpoints
- Leaking DB errors or stack traces to clients
- Missing rate limiting on unauthenticated endpoints
- Defaulting to REST when GraphQL or tRPC is the right fit

## Resource Index

- REST design and resource modeling
- GraphQL schema and resolver patterns
- TypeScript-specific patterns (tRPC)
- Authentication methods comparison
- Security testing checklist
- API versioning strategies

## Limitations
- Use this skill only when the task clearly matches the scope described above.
- Do not treat the output as a substitute for environment-specific validation, testing, or expert review.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
