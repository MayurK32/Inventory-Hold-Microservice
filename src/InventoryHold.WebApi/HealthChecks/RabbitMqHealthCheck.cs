using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace InventoryHold.WebApi.HealthChecks;

public sealed class RabbitMqHealthCheck(IConnection connection) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            return Task.FromResult(connection.IsOpen
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("RabbitMQ connection is closed."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(ex.Message, ex));
        }
    }
}
