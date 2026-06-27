---
name: azure-resource-manager-redis-dotnet
description: "Management plane SDK for provisioning and managing Azure Cache for Redis resources via Azure Resource Manager using the Azure.ResourceManager.Redis .NET package."
risk: safe
source: community
date_added: "2026-02-27"
---

# Azure.ResourceManager.Redis (.NET)

Management plane SDK for provisioning and managing Azure Cache for Redis resources via Azure Resource Manager.

> **Management vs Data Plane**
> - **This SDK (Azure.ResourceManager.Redis)**: Create caches, configure firewall rules, manage access keys, set up geo-replication
> - **Data Plane SDK (StackExchange.Redis)**: Get/set keys, pub/sub, streams, Lua scripts

## Installation

```bash
dotnet add package Azure.ResourceManager.Redis
dotnet add package Azure.Identity
```

**Current Version**: 1.5.1 (Stable) | **API Version**: 2024-11-01

## Authentication

```csharp
var credential = new DefaultAzureCredential();
var armClient = new ArmClient(credential);
var subscription = armClient.GetSubscriptionResource(
    new ResourceIdentifier($"/subscriptions/{Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID")}"));
```

## Core Workflows

### Create Redis Cache

```csharp
var cacheData = new RedisCreateOrUpdateContent(
    location: AzureLocation.EastUS,
    sku: new RedisSku(RedisSkuName.Standard, RedisSkuFamily.BasicOrStandard, 1))
{
    EnableNonSslPort = false,
    MinimumTlsVersion = RedisTlsVersion.Tls1_2
};

var operation = await cacheCollection.CreateOrUpdateAsync(
    WaitUntil.Completed, "my-redis-cache", cacheData);
```

### Get Access Keys

```csharp
var keys = await cache.Value.GetKeysAsync();
// Use keys.Value.PrimaryKey with StackExchange.Redis
```

## SKU Reference

| SKU | Use Case |
|-----|----------|
| Basic | Dev/test only — no SLA, single node |
| Standard | Production — primary/replica, SLA |
| Premium | Clustering, geo-replication, VNet, persistence |

## Best Practices

1. Use `WaitUntil.Completed` for operations that must finish before proceeding
2. Always use `DefaultAzureCredential` — never hardcode keys
3. Enable TLS 1.2 minimum; disable non-SSL port
4. Use Premium SKU for production (geo-replication, clustering, persistence)
5. Rotate keys regularly with `RegenerateKeyAsync`

## Connecting with StackExchange.Redis (Data Plane)

```csharp
var connectionString = $"{cache.Value.Data.HostName}:{cache.Value.Data.SslPort},password={keys.Value.PrimaryKey},ssl=True,abortConnect=False";
var connection = ConnectionMultiplexer.Connect(connectionString);
```

## Limitations
- Use this skill only when the task clearly matches the scope described above.
- Do not treat the output as a substitute for environment-specific validation, testing, or expert review.
- Stop and ask for clarification if required inputs, permissions, safety boundaries, or success criteria are missing.
