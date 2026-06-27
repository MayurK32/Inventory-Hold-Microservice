---
name: docker-expert
description: "Advanced containerization guidance covering Dockerfile optimization, multi-stage builds, security hardening, and Docker Compose orchestration for production deployments."
risk: safe
source: community
date_added: "2026-02-27"
---

# Docker Expert

Advanced containerization guidance covering Dockerfile optimization, multi-stage builds, security hardening, and Docker Compose orchestration.

## Use this skill when

- Writing or optimizing Dockerfiles (multi-stage builds, layer caching)
- Hardening container security (non-root users, minimal base images)
- Composing multi-service environments with Docker Compose
- Setting up health checks and service dependency ordering
- Minimizing production image sizes (distroless, alpine, artifact copying)
- Enabling hot-reload development workflows with volume mounts

## Do not use this skill when

- You need Kubernetes orchestration (use `kubernetes-expert`)
- You need GitHub Actions CI/CD (use `github-actions-expert`)
- You need cloud provider setup (use `devops-expert`)
- You need database containerization specifics (use `database-expert`)

## Key Expertise Areas

### Dockerfile Optimization
- Layer caching: separate dependency installation from source code copying
- Multi-stage builds to minimize production image sizes
- `.dockerignore` to exclude build artifacts and secrets

### Security Hardening
- Non-root user with specific UID/GID
- Secrets management via Docker secrets (never ENV vars for sensitive values)
- Minimal base images (alpine, distroless) to reduce attack surface

### Docker Compose
- Service dependency management with `depends_on: condition: service_healthy`
- Health check definitions per service
- Custom network configuration and named volumes
- Environment-specific overrides (docker-compose.override.yml)

### Image Size Optimization
- Distroless images for final stage
- Strategic artifact copying from build stage
- Multi-stage: build → publish → final

### Development Workflow
- Volume mounts for hot reloading source code
- Debug port exposure
- Environment-specific targets (`--target development`)

## Diagnostic Approach

- Read Dockerfile and compose files first (Read, Grep, Glob)
- Validate via build testing and security scanning
- Verify health checks pass before declaring done

## Limitations
- Defers to specialized agents for Kubernetes, CI/CD, and cloud services.
- Use this skill only when the task clearly matches the scope described above.
- Do not treat the output as a substitute for environment-specific validation, testing, or expert review.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
