using InventoryHold.Contracts.Settings;
using InventoryHold.Domain.Cache;
using InventoryHold.Domain.Messaging;
using InventoryHold.Domain.Repositories;
using InventoryHold.Domain.Transactions;
using InventoryHold.Infrastructure.Persistence;
using InventoryHold.Infrastructure.Persistence.Documents;
using InventoryHold.Infrastructure.Transactions;
using InventoryHold.WebApi.Endpoints;
using InventoryHold.WebApi.Middleware;
using InventoryHold.WebApi.Services;
using InventoryHold.Infrastructure.Caching;
using InventoryHold.Infrastructure.Messaging;
using InventoryHold.WebApi.Stubs;
using StackExchange.Redis;
using RabbitMQ.Client;
using InventoryHold.WebApi.Workers;
using MongoDB.Driver;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Config binding
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDb"));
builder.Services.Configure<RedisSettings>(builder.Configuration.GetSection("Redis"));
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<HoldSettings>(builder.Configuration.GetSection("HoldSettings"));

// OpenAPI
builder.Services.AddOpenApi();

// Phase 3: MongoDB
var connectionString = builder.Configuration.GetValue<string>("MongoDb:ConnectionString")!;
var databaseName     = builder.Configuration.GetValue<string>("MongoDb:DatabaseName")!;
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoDatabase>().GetCollection<HoldDocument>("holds"));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoDatabase>().GetCollection<InventoryDocument>("inventory"));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoDatabase>().GetCollection<AppSettingDocument>("settings"));
builder.Services.AddSingleton<IHoldRepository, MongoHoldRepository>();
builder.Services.AddSingleton<IInventoryRepository, MongoInventoryRepository>();
builder.Services.AddSingleton<ISettingsRepository, MongoSettingsRepository>();
builder.Services.AddSingleton<ITransactionFactory, MongoTransactionFactory>();
builder.Services.AddSingleton<CollectionIndexInitializer>();
builder.Services.AddSingleton<DatabaseSeeder>();

// Phase 9: Redis
var redisConnectionString = builder.Configuration.GetValue<string>("Redis:ConnectionString")!;
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddSingleton<IInventoryCache, RedisCacheService>();

// Phase 8: RabbitMQ
var rabbitMqSettings = builder.Configuration.GetSection("RabbitMq").Get<RabbitMqSettings>()!;
builder.Services.AddSingleton<IConnection>(_ =>
    RabbitMqConnectionFactory.CreateAsync(rabbitMqSettings).GetAwaiter().GetResult());
builder.Services.AddSingleton<RabbitMqTopologyInitializer>();
builder.Services.AddSingleton<IHoldEventPublisher, RabbitMqHoldEventPublisher>();
builder.Services.AddHostedService<HoldExpiryWorker>();
// TODO Phase 10: Health checks — MongoDB, Redis, RabbitMQ

// Phase 4: exception handler + hold service
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddScoped<HoldService>();
builder.Services.AddScoped<InventoryService>();

builder.Services.AddProblemDetails();

var app = builder.Build();

// Phase 3: startup pipeline
await app.Services.GetRequiredService<CollectionIndexInitializer>().InitializeAsync();
await app.Services.GetRequiredService<DatabaseSeeder>().SeedAsync();
await app.Services.GetRequiredService<RabbitMqTopologyInitializer>().InitializeAsync();

// Middleware pipeline (order is fixed)
app.UseExceptionHandler();
app.UseStatusCodePages();

// OpenAPI UI at /scalar
app.MapOpenApi();
app.MapScalarApiReference();

// Phase 4: endpoints
app.MapHoldEndpoints();
app.MapInventoryEndpoints();

// Temp health stub — replaced with real checks in Phase 10
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
   .ExcludeFromDescription();

app.Run();
