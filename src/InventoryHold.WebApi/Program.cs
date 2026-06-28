using InventoryHold.Contracts.Settings;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Config binding
builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDb"));
builder.Services.Configure<RedisSettings>(builder.Configuration.GetSection("Redis"));
builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<HoldSettings>(builder.Configuration.GetSection("HoldSettings"));

// OpenAPI
builder.Services.AddOpenApi();

// TODO Phase 3: MongoDB — IMongoClient, repositories, seeder, index initializer
// TODO Phase 9: Redis — IConnectionMultiplexer, RedisCacheService
// TODO Phase 8: RabbitMQ — RabbitMqConnectionFactory, topology, IHoldEventPublisher
// TODO Phase 5: Background worker — HoldExpiryWorker
// TODO Phase 10: Health checks — MongoDB, Redis, RabbitMQ

builder.Services.AddProblemDetails();

var app = builder.Build();

// Middleware pipeline (order is fixed)
app.UseExceptionHandler();
app.UseStatusCodePages();

// OpenAPI UI at /scalar
app.MapOpenApi();
app.MapScalarApiReference();

// TODO Phase 4-9: Endpoint registrations

// Temp health stub — replaced with real checks in Phase 10
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
   .ExcludeFromDescription();

app.Run();
