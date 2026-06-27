---
name: agent-orchestrator
description: "Meta-skill functioning as a central coordination layer for a multi-agent ecosystem. Provides automated skill discovery, intelligent matching, and workflow orchestration."
risk: safe
source: community
date_added: "2026-02-27"
---

# Agent Orchestrator

Meta-skill providing automated skill discovery, intelligent matching, and workflow orchestration across a multi-agent ecosystem.

## Use this skill when

- Routing a request to the most appropriate skill(s)
- Coordinating multiple skills in a pipeline or parallel workflow
- Discovering available skills for a given task category
- Orchestrating complex multi-step tasks across specialized agents

## Do not use this skill when

- The task maps clearly to a single known skill (invoke it directly)
- You need infrastructure or deployment work

## Core Principle

**Always scan before processing.** Before handling any request, scan the skill registry to find the best match. New skills self-register via SKILL.md creation; removed skills auto-exclude.

## Skill Matching Algorithm

Skills ranked by relevance using a points-based system:
- Name match: +15 points
- Trigger keyword match: +10 points
- Capability match: +5 points
- Word overlap: +1 point per word

## Workflow Patterns

### Sequential Pipeline
Tasks flow one-after-another: A → B → C. Use when each step depends on the previous.

### Parallel Execution
Independent tasks run simultaneously. Use when steps have no dependency.

### Primary-with-Support
One primary skill leads; support skills provide supplementary output.

## Orchestration Trigger

2+ matched skills for a request → orchestration step is required. Single match → invoke directly.

## Registry

- Skills catalogued in `agent-orchestrator/data/registry.json`
- Project assignments tracked in `projects.json`
- Skills have metadata: name, location, capabilities, language, status (active/incomplete/missing)

## Mandatory Three-Step Process

Every user request follows:
1. Scan registry
2. Match skills by relevance score
3. Orchestrate if 2+ skills matched

## Limitations
- Use this skill only when the task clearly matches the scope described above.
- Do not treat the output as a substitute for environment-specific validation, testing, or expert review.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
