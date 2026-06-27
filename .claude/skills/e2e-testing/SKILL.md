---
name: e2e-testing
description: "End-to-end testing workflow with Playwright for browser automation, visual regression, cross-browser testing, and CI/CD integration."
category: granular-workflow-bundle
risk: safe
source: personal
date_added: "2026-02-27"
---

# E2E Testing Workflow

Specialized workflow for end-to-end testing using Playwright — browser automation, visual regression testing, cross-browser testing, and CI/CD integration.

## When to Use This Workflow

- Setting up E2E testing from scratch
- Automating browser tests for critical user flows
- Implementing visual regression to catch UI regressions
- Testing across Chromium, Firefox, and WebKit
- Integrating E2E tests with CI/CD pipelines

## Workflow Phases

### Phase 1: Test Setup
- Install Playwright and configure test framework
- Set up test directory structure
- Configure browsers and base URL

### Phase 2: Test Design
- Identify critical flows (happy path + key error cases)
- Design test scenarios and plan test data
- Create page object models for reusable locators

### Phase 3: Test Implementation
- Write test scripts with proper assertions
- Implement waits for async operations
- Handle dynamic content and loading states

### Phase 4: Browser Automation
- Configure headless mode for CI
- Set up screenshots and video recording on failure
- Add trace collection for debugging

### Phase 5: Visual Regression
- Create baseline screenshot snapshots
- Add visual assertions with configured thresholds
- Review and approve snapshot diffs

### Phase 6: Cross-Browser Testing
- Run tests across Chromium, Firefox, WebKit
- Test mobile viewport emulation

### Phase 7: CI/CD Integration
- Create GitHub Actions workflow
- Configure parallel test execution
- Set up test artifacts (screenshots, videos, reports)
- Add Slack/email notifications on failure

## Quality Gates

- [ ] Tests passing on all configured browsers
- [ ] Critical flows covered (create hold, release hold, stock-out)
- [ ] Visual snapshots stable
- [ ] CI integration working with artifact upload
- [ ] Flaky test rate < 1%

## Limitations
- Use this skill only when the task clearly matches the scope described above.
- Do not treat the output as a substitute for environment-specific validation, testing, or expert review.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
