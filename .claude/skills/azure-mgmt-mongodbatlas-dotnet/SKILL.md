---
name: azure-mgmt-mongodbatlas-dotnet
description: ".NET SDK for managing MongoDB Atlas Organizations as Azure ARM resources with Azure Marketplace billing integration."
risk: safe
source: community
date_added: "2026-02-27"
---

# Azure.ResourceManager.MongoDBAtlas (.NET)

.NET SDK for managing MongoDB Atlas Organizations as Azure ARM resources with Azure Marketplace billing integration.

## Installation

```bash
dotnet add package Azure.ResourceManager.MongoDBAtlas
dotnet add package Azure.Identity
dotnet add package Azure.ResourceManager
```

## Scope

This SDK manages MongoDB Atlas **Organizations** through Azure Resource Manager. It does NOT directly manage:
- Atlas clusters
- Databases
- Collections
- Users/roles

For those resources, use the MongoDB Atlas API after organization creation.

## Authentication

Uses `DefaultAzureCredential` from the Azure.Identity package.

```csharp
var credential = new DefaultAzureCredential();
var armClient = new ArmClient(credential);
```

## Primary Operations

- **Create** — Construct `MongoDBAtlasOrganizationData` with marketplace details and execute a long-running operation
- **Get** — Fetch by resource identifier or enumerate at subscription level
- **Update** — Tag management and property modifications via patch operations
- **Delete** — Async removal operations

## Provisioning States

Monitor these states during operations:
- `Succeeded` — resource ready
- `Provisioning` — creation in progress
- `Updating` — modification in progress
- `Deleting` — removal in progress

Marketplace subscription states: `PendingFulfillmentStart`, `Subscribed`, `Suspended`, `Unsubscribed`

## Best Practices

- Always use async methods
- Properly handle long-running operations with `WaitUntil` parameters
- Verify provisioning state is `Succeeded` before considering resources ready
- Use `DefaultAzureCredential` — never hardcode credentials

## Limitations
- Does not manage Atlas clusters, databases, collections, or users — use Atlas API for those.
- Use this skill only when the task clearly matches the scope described above.
- Do not treat the output as a substitute for environment-specific validation, testing, or expert review.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
