---
name: application-performance-performance-optimization
description: "Coordinate performance improvements across the full stack — backend, frontend, and infrastructure — using data-driven profiling and observability."
risk: safe
source: community
date_added: "2026-02-27"
---

# Application Performance Optimization

Coordinate performance improvements across the entire application stack through structured profiling, optimization, and validation phases.

## Use this skill when

- Establishing performance baselines for a system
- Designing load tests and defining SLOs
- Building observability systems (metrics, traces, dashboards)
- Optimizing database queries, backend services, or frontend bundles
- Managing performance improvements across multiple application layers

## Do not use this skill when

- You need an isolated single-file fix (just fix it directly)
- No profiling data or baselines are available yet
- You need infrastructure provisioning rather than optimization

## Workflow Phases

### Phase 1: Baseline
- Profile current state through observability assessment
- Establish baseline metrics (P50, P95, P99 latency; throughput; error rate)
- Identify bottlenecks (CPU, memory, DB, network, frontend paint times)

### Phase 2: Backend Optimization
- Database query analysis (N+1, missing indexes, slow queries)
- Caching strategy (Redis, in-memory, CDN)
- Async and concurrency improvements
- Connection pool tuning

### Phase 3: Frontend Optimization
- Bundle analysis and code splitting
- Lazy loading and tree shaking
- CDN and asset optimization
- Core Web Vitals (LCP < 2.5s, FID < 100ms, CLS < 0.1)

### Phase 4: Validation
- Load testing to confirm improvements
- Automated regression detection in CI
- Rollback plan before deploying

### Phase 5: Monitoring
- Ongoing alerting on SLO violations
- Scheduled performance review cadence

## Critical Safeguards

- **Avoid load testing production without approvals and safeguards**
- Roll changes gradually with rollback capability
- Measure before and after every optimization — no guessing

## Success Targets

- P50 < 200ms response times
- LCP < 2.5s for user-facing pages
- Error rate < 0.1% under normal load

## Limitations
- Use this skill only when the task clearly matches the scope described above.
- Do not treat the output as a substitute for environment-specific validation, testing, or expert review.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
