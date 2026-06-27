---
name: dotnet-backend
description: ASP.NET Core & Enterprise API Expert for building production-grade backend services using .NET 8+.
risk: safe
source: anthropic
date_added: '2026-02-27'
---

# dotnet-backend

ASP.NET Core & Enterprise API Expert. Build production-grade backend services using .NET 8+ with best-in-class patterns.

## Use this skill when

- Building ASP.NET Core Web APIs (Minimal APIs or controller-based)
- Implementing authentication/authorization with JWT, Identity, OAuth 2.0
- Creating background services and scheduled jobs (IHostedService)
- Designing RESTful APIs with proper validation and error handling
- Implementing health checks, structured logging, and observability
- Writing unit and integration tests with xUnit/NUnit

## Do not use this skill when

- The project is not using .NET or C#
- You only need frontend implementation guidance
- You need cloud-provider-specific deployment details

## Instructions

1. Define API surface, request/response contracts, and validation rules.
2. Implement with Minimal APIs or controllers based on complexity.
3. Add authentication, health checks, and structured logging.
4. Write tests covering happy path and error scenarios.

## Key Capabilities

### ASP.NET Core APIs
- Minimal APIs and controller-based approaches
- Middleware pipeline and request processing
- FluentValidation for request validation
- Swagger/OpenAPI documentation

### Background Services
- IHostedService and BackgroundService implementations
- Polling workers with configurable intervals
- Graceful shutdown with CancellationToken

### Data Access
- Entity Framework Core 8+ and Dapper
- Repository pattern and Unit of Work
- Async data access throughout

### Observability
- Structured logging with Serilog
- Health checks for dependencies
- OpenTelemetry integration

## Limitations
- Assumes modern .NET (ASP.NET Core 8+)
- Does not cover frontend implementations
- Use this skill only when the task clearly matches the scope described above.
