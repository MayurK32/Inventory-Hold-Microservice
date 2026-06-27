---
name: api-documentation-generator
description: "Generate professional API documentation automatically from code — endpoints, parameters, request/response examples, error codes, and OpenAPI specifications."
risk: safe
source: community
date_added: "2026-02-27"
---

# API Documentation Generator

Generate professional, developer-friendly API documentation from code. Covers endpoints, parameters, examples, error codes, and OpenAPI specifications.

## Use this skill when

- Generating OpenAPI/Swagger documentation from code
- Creating Postman collections for API testing
- Writing endpoint documentation with request/response schemas
- Documenting authentication setup and rate limits
- Creating getting-started guides for API consumers

## Do not use this skill when

- Documentation already exists and only needs minor edits
- You need infrastructure or deployment documentation

## Documentation Steps

1. **Structure Analysis** — examine endpoints, HTTP methods, parameters, auth requirements
2. **Endpoint Documentation** — generate detailed specs with request/response formats
3. **Usage Guidelines** — add getting-started guides and best practices
4. **Error Handling** — document all possible error codes and resolution steps
5. **Interactive Examples** — create Postman collections and OpenAPI specs

## What to Include Per Endpoint

```yaml
# Example endpoint doc
POST /api/holds:
  summary: Create an inventory hold
  requestBody:
    required: true
    content:
      application/json:
        schema:
          $ref: '#/components/schemas/CreateHoldRequest'
        example:
          customerName: "John Doe"
          items:
            - productId: "widget-a"
              quantity: 2
  responses:
    '201':
      description: Hold created successfully
    '400':
      description: Validation error
    '409':
      description: Insufficient stock or write conflict
    '422':
      description: Invalid request body
```

## Best Practices

- Maintain consistency across all endpoints
- Include realistic, working examples
- Document every error scenario with resolution guidance
- Keep docs synchronized with code (auto-generate from attributes/annotations)
- Provide Postman collections for easy testing

## Common Mistakes to Avoid

- Outdated or broken examples
- Vague descriptions ("Returns data")
- Missing error documentation
- Incomplete authentication details
- No mention of rate limits

## Limitations
- Use this skill only when the task clearly matches the scope described above.
- Do not treat the output as a substitute for environment-specific validation, testing, or expert review.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
