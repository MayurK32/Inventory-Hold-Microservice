---
name: csharp-pro
description: "Write modern C# code with advanced features like records, pattern matching, and async/await. Optimizes .NET applications, implements enterprise patterns, and ensures comprehensive testing."
risk: unknown
source: community
date_added: "2026-02-27"
---

# C# Pro

Write modern, idiomatic C# code leveraging the latest language features. Optimize .NET applications and implement enterprise-grade patterns with comprehensive testing.

## Use this skill when

- Writing or modernizing C# code using records, pattern matching, nullable references
- Implementing async/await patterns correctly without deadlocks
- Applying SOLID principles and composition-based design
- Setting up testing with xUnit, NUnit, Moq, and FluentAssertions
- Optimizing performance with Span<T>, Memory<T>, and async streams
- Implementing enterprise and microservices architecture patterns

## Do not use this skill when

- The project is not using .NET or C#
- You need frontend or UI implementation guidance
- You need cloud-provider-specific infrastructure

## Instructions

- Apply modern C# syntax and idioms.
- Use composition over inheritance; favor records for DTOs.
- Enforce null safety with nullable reference types.
- Use non-blocking async patterns throughout.
- Write comprehensive unit tests for all business logic.

## Core Expertise Areas

### Modern Language Features
- Records, primary constructors, collection expressions (C# 12/13)
- Pattern matching: switch expressions, property patterns, list patterns
- Nullable reference types and null-forgiving operators
- Init-only setters and with-expressions

### Async & Concurrency
- ValueTask vs Task selection
- IAsyncEnumerable for streaming
- ConfigureAwait(false) in library code
- CancellationToken propagation

### Performance
- Span<T>, Memory<T> for zero-allocation patterns
- ArrayPool<T> and stackalloc
- Avoiding boxing and unnecessary allocations
- BenchmarkDotNet profiling

### Testing
- xUnit and NUnit test frameworks
- Moq and NSubstitute for mocking
- FluentAssertions for readable assertions
- WebApplicationFactory for integration tests

## Limitations
- Use this skill only when the task clearly matches the scope described above.
- Does not replace environment-specific validation, real-world testing, or professional review.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
