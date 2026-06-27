---
name: api-security-best-practices
description: "Comprehensive guidance for securing REST, GraphQL, and WebSocket APIs against OWASP API Top 10 vulnerabilities."
risk: safe
source: community
date_added: "2026-02-27"
---

# API Security Best Practices

Comprehensive guidance for securing REST, GraphQL, and WebSocket APIs. Covers authentication, input validation, rate limiting, data protection, and OWASP API Top 10.

## Use this skill when

- Implementing API authentication and authorization (JWT, OAuth 2.0, API keys)
- Adding input validation to prevent injection attacks
- Setting up rate limiting and DDoS protection
- Auditing APIs against OWASP API Security Top 10
- Configuring HTTPS/TLS and secure headers

## Do not use this skill when

- You need application penetration testing (use a security specialist)
- You need network-level security (firewalls, WAF configuration)

## Core Security Areas

### Authentication & Authorization
- JWT with 256-bit minimum secret; include refresh token rotation
- OAuth 2.0 for third-party integrations
- Role-based access control (RBAC)
- Multi-factor authentication for admin endpoints

### Input Validation
- Validate all input data — type, length, format, range
- Use parameterized queries/ORMs to prevent SQL injection
- Sanitize output to prevent XSS
- Reject unexpected fields (strict schema validation)

### Rate Limiting
- Per-user and per-IP limits using Redis
- Stricter thresholds on authentication endpoints (prevent brute force)
- Return `429 Too Many Requests` with `Retry-After` header

### Data Protection
- Enforce HTTPS/TLS; redirect HTTP → HTTPS
- Hash passwords with bcrypt (≥10 salt rounds) or Argon2
- Sanitize error messages — never expose stack traces or DB details
- Use RFC 7807 ProblemDetails for structured error responses

### Security Checklist
- [ ] Auth required on all protected routes
- [ ] Input validated on all fields
- [ ] SQL injection prevention (parameterized queries)
- [ ] Rate limiting on public endpoints
- [ ] No sensitive data in responses (passwords, tokens, PII)
- [ ] CORS configured correctly
- [ ] Request size limits set
- [ ] OWASP API Top 10 reviewed

## Critical Rule

**"Don't Use Weak Secrets"** — JWT secrets must be 256-bit minimum. Store in environment variables, never in code.

## Limitations
- Security requires ongoing maintenance — regular audits, dependency updates, vulnerability monitoring.
- Use this skill only when the task clearly matches the scope described above.
- Do not treat the output as a substitute for a professional security audit.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
