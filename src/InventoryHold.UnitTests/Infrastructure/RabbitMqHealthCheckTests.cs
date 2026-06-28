using FluentAssertions;
using InventoryHold.WebApi.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using RabbitMQ.Client;

namespace InventoryHold.UnitTests.Infrastructure;

public class RabbitMqHealthCheckTests
{
    private readonly Mock<IConnection> _connection = new();
    private readonly HealthCheckContext _context = new()
    {
        Registration = new HealthCheckRegistration("rabbitmq", Mock.Of<IHealthCheck>(), null, null)
    };

    [Fact]
    public async Task IsOpen_ReturnsHealthy()
    {
        _connection.Setup(c => c.IsOpen).Returns(true);
        var check = new RabbitMqHealthCheck(_connection.Object);

        var result = await check.CheckHealthAsync(_context);

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task IsClosed_ReturnsUnhealthy()
    {
        _connection.Setup(c => c.IsOpen).Returns(false);
        var check = new RabbitMqHealthCheck(_connection.Object);

        var result = await check.CheckHealthAsync(_context);

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task ThrowsException_ReturnsUnhealthy()
    {
        _connection.Setup(c => c.IsOpen).Throws(new InvalidOperationException("connection lost"));
        var check = new RabbitMqHealthCheck(_connection.Object);

        var result = await check.CheckHealthAsync(_context);

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Exception.Should().BeOfType<InvalidOperationException>();
    }
}
