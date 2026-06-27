---
name: api-endpoint-builder
description: "Builds production-ready REST API endpoints with validation, error handling, authentication, and documentation. Follows best practices for security and scalability."
category: development
risk: safe
source: community
date_added: "2026-03-05"
---

# API Endpoint Builder

Build complete, production-ready REST API endpoints with proper validation, error handling, authentication, and documentation.

## When to Use This Skill

- User asks to "create an API endpoint" or "build a REST API"
- Building new backend features
- Adding endpoints to existing APIs
- User mentions "API", "endpoint", "route", or "REST"
- Creating CRUD operations

## HTTP Status Codes

- `200` - Success (GET, PUT, PATCH)
- `201` - Created (POST)
- `204` - No Content (DELETE)
- `400` - Bad Request (validation failed)
- `401` - Unauthorized (not authenticated)
- `403` - Forbidden (not authorized)
- `404` - Not Found
- `409` - Conflict (duplicate / write conflict)
- `410` - Gone (resource existed but permanently deleted)
- `422` - Unprocessable Entity (semantic validation failure)
- `429` - Too Many Requests (rate limit)
- `500` - Internal Server Error

## Response Format

```json
// Success
{ "data": { ... } }

// Error (RFC 7807 ProblemDetails)
{
  "type": "https://example.com/problems/not-found",
  "title": "Resource not found",
  "status": 404,
  "detail": "Hold 550e8400 does not exist.",
  "instance": "/api/holds/550e8400"
}

// List with pagination
{
  "data": [...],
  "pagination": { "page": 1, "pageSize": 20, "total": 100, "totalPages": 5 }
}
```

## Security Checklist

- [ ] Authentication required for protected routes
- [ ] Authorization checks (user owns resource)
- [ ] Input validation on all fields
- [ ] Parameterized queries (no SQL injection)
- [ ] Rate limiting on public endpoints
- [ ] No sensitive data in responses
- [ ] CORS configured properly
- [ ] Request size limits set

## Common Patterns

### CRUD Operations
```
POST   /api/resources          → 201 Created
GET    /api/resources          → 200 OK (paginated list)
GET    /api/resources/:id      → 200 OK | 404 Not Found
PUT    /api/resources/:id      → 200 OK | 404 | 409
DELETE /api/resources/:id      → 200 OK | 404 | 410
```

### Pagination Query Parameters
```
?page=1&pageSize=20&sortBy=createdAt&sortDir=desc&status=Active
```

## Limitations
- Use this skill only when the task clearly matches the scope described above.
- Do not treat the output as a substitute for environment-specific validation, testing, or expert review.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
