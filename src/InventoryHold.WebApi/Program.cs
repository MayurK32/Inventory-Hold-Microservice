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
using InventoryHold.WebApi.HealthChecks;
using InventoryHold.WebApi.Stubs;
using StackExchange.Redis;
using RabbitMQ.Client;
using InventoryHold.WebApi.Workers;
using MongoDB.Driver;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext());

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
    builder.Services.AddScoped<IHoldRepository, MongoHoldRepository>();
    builder.Services.AddScoped<IInventoryRepository, MongoInventoryRepository>();
    builder.Services.AddScoped<ISettingsRepository, MongoSettingsRepository>();
    builder.Services.AddScoped<ITransactionFactory, MongoTransactionFactory>();
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
        Task.Run(() => RabbitMqConnectionFactory.CreateAsync(rabbitMqSettings)).GetAwaiter().GetResult());
    builder.Services.AddSingleton<RabbitMqTopologyInitializer>();
    builder.Services.AddSingleton<IHoldEventPublisher, RabbitMqHoldEventPublisher>();
    builder.Services.AddHostedService<HoldExpiryWorker>();

    // Phase 10: Health checks
    builder.Services.AddHealthChecks()
        .AddMongoDb(sp => sp.GetRequiredService<IMongoClient>(), tags: ["mongodb"])
        .AddRedis(sp => sp.GetRequiredService<IConnectionMultiplexer>(), tags: ["redis"])
        .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["rabbitmq"]);

    // Phase 4: exception handler + services
    builder.Services.AddExceptionHandler<DomainExceptionHandler>();
    builder.Services.AddScoped<HoldService>();
    builder.Services.AddScoped<InventoryService>();

    builder.Services.AddProblemDetails();

    builder.Services.AddRateLimiter(opts =>
    {
        opts.AddFixedWindowLimiter("holds-create", o =>
        {
            o.PermitLimit = 100;
            o.Window = TimeSpan.FromMinutes(1);
            o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            o.QueueLimit = 0;
        });
        opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    var app = builder.Build();

    // Startup pipeline
    await app.Services.GetRequiredService<CollectionIndexInitializer>().InitializeAsync();
    await app.Services.GetRequiredService<DatabaseSeeder>().SeedAsync();
    await app.Services.GetRequiredService<RabbitMqTopologyInitializer>().InitializeAsync();

    // Middleware pipeline (order is fixed)
    app.UseRateLimiter();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(opts =>
        opts.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms");
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    // OpenAPI UI at /scalar
    app.MapOpenApi();
    app.MapScalarApiReference();

    // Phase 4: endpoints
    app.MapHoldEndpoints();
    app.MapInventoryEndpoints();

    // Phase 10: Health endpoint
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = async (ctx, report) =>
        {
            ctx.Response.ContentType = "application/json";
            object result = report.Status == HealthStatus.Healthy
                ? new { status = "Healthy" }
                : new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())
                };
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(result));
        }
    }).ExcludeFromDescription();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}
