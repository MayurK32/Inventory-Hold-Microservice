using System.Text.Json;
using FluentAssertions;
using InventoryHold.Domain.Entities;
using InventoryHold.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RabbitMQ.Client;

namespace InventoryHold.UnitTests.Infrastructure;

public class RabbitMqPublisherTests
{
    private readonly Mock<IConnection> _connection = new();
    private readonly Mock<IChannel>    _channel    = new();
    private readonly RabbitMqHoldEventPublisher _publisher;

    public RabbitMqPublisherTests()
    {
        _connection
            .Setup(c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_channel.Object);

        _channel
            .Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        _publisher = new RabbitMqHoldEventPublisher(
            _connection.Object, NullLogger<RabbitMqHoldEventPublisher>.Instance);
    }

    private static Hold MakeHold() =>
        Hold.Reconstruct("hold-1", "Alice", HoldStatus.Active,
            [new HoldItem("widget-a", "Widget A", 3)],
            DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(10), null, null);

    private byte[] CaptureBody()
    {
        byte[] capturedBody = [];
        _channel
            .Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                (_, _, _, _, body, _) => capturedBody = body.ToArray())
            .Returns(ValueTask.CompletedTask);
        return capturedBody;
    }

    [Fact]
    public async Task PublishHoldCreatedAsync_CorrectRoutingKeyAndBody()
    {
        byte[] capturedBody = [];
        _channel
            .Setup(c => c.BasicPublishAsync(
                "inventory.hold.events", "hold.created", It.IsAny<bool>(),
                It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                (_, _, _, _, body, _) => capturedBody = body.ToArray())
            .Returns(ValueTask.CompletedTask);

        var hold = MakeHold();
        await _publisher.PublishHoldCreatedAsync(hold);

        _channel.Verify(c => c.BasicPublishAsync(
            "inventory.hold.events", "hold.created", It.IsAny<bool>(),
            It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        var json = JsonSerializer.Deserialize<JsonElement>(capturedBody);
        json.GetProperty("holdId").GetString().Should().Be("hold-1");
        json.TryGetProperty("expiresAt", out _).Should().BeTrue();
        json.TryGetProperty("releasedAt", out _).Should().BeFalse();
        json.TryGetProperty("expiredAt", out _).Should().BeFalse();
    }

    [Fact]
    public async Task PublishHoldReleasedAsync_CorrectRoutingKeyAndBody()
    {
        byte[] capturedBody = [];
        _channel
            .Setup(c => c.BasicPublishAsync(
                "inventory.hold.events", "hold.released", It.IsAny<bool>(),
                It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                (_, _, _, _, body, _) => capturedBody = body.ToArray())
            .Returns(ValueTask.CompletedTask);

        var releasedAt = DateTime.UtcNow;
        var hold = Hold.Reconstruct("hold-1", "Alice", HoldStatus.Released,
            [new HoldItem("widget-a", "Widget A", 3)],
            DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(5), releasedAt, null);

        await _publisher.PublishHoldReleasedAsync(hold);

        _channel.Verify(c => c.BasicPublishAsync(
            "inventory.hold.events", "hold.released", It.IsAny<bool>(),
            It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        var json = JsonSerializer.Deserialize<JsonElement>(capturedBody);
        json.TryGetProperty("releasedAt", out _).Should().BeTrue();
        json.TryGetProperty("expiredAt", out _).Should().BeFalse();
        json.TryGetProperty("expiresAt", out _).Should().BeFalse();
    }

    [Fact]
    public async Task PublishHoldExpiredAsync_CorrectRoutingKeyAndBody()
    {
        byte[] capturedBody = [];
        _channel
            .Setup(c => c.BasicPublishAsync(
                "inventory.hold.events", "hold.expired", It.IsAny<bool>(),
                It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, bool, BasicProperties, ReadOnlyMemory<byte>, CancellationToken>(
                (_, _, _, _, body, _) => capturedBody = body.ToArray())
            .Returns(ValueTask.CompletedTask);

        var expiredAt = DateTime.UtcNow;
        var hold = Hold.Reconstruct("hold-1", "Alice", HoldStatus.Expired,
            [new HoldItem("widget-a", "Widget A", 3)],
            DateTime.UtcNow.AddMinutes(-20), DateTime.UtcNow.AddMinutes(-5), null, expiredAt);

        await _publisher.PublishHoldExpiredAsync(hold);

        _channel.Verify(c => c.BasicPublishAsync(
            "inventory.hold.events", "hold.expired", It.IsAny<bool>(),
            It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<CancellationToken>()), Times.Once);

        var json = JsonSerializer.Deserialize<JsonElement>(capturedBody);
        json.TryGetProperty("expiredAt", out _).Should().BeTrue();
        json.TryGetProperty("releasedAt", out _).Should().BeFalse();
        json.TryGetProperty("expiresAt", out _).Should().BeFalse();
    }

    [Fact]
    public async Task PublishAsync_ChannelThrows_LogsErrorAndDoesNotRethrow()
    {
        var mockLogger = new Mock<ILogger<RabbitMqHoldEventPublisher>>();
        var publisher = new RabbitMqHoldEventPublisher(_connection.Object, mockLogger.Object);

        _channel
            .Setup(c => c.BasicPublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<BasicProperties>(), It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromException(new Exception("RabbitMQ down")));

        await FluentActions.Invoking(() => publisher.PublishHoldCreatedAsync(MakeHold()))
            .Should().NotThrowAsync();

        mockLogger.Verify(
            x => x.Log(LogLevel.Error, It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
