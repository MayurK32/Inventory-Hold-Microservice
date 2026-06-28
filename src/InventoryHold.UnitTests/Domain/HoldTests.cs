using FluentAssertions;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Exceptions;

namespace InventoryHold.UnitTests.Domain;

public class HoldTests
{
    private static HoldItem[] OneItem() => [new HoldItem("widget-a", "Widget A", 1)];

    [Fact]
    public void Create_ValidInput_SetsId_StatusActive_Timestamps()
    {
        var before = DateTime.UtcNow;
        var hold = Hold.Create(null, OneItem(), expirationMinutes: 15);

        Guid.TryParse(hold.Id, out _).Should().BeTrue();
        hold.Status.Should().Be(HoldStatus.Active);
        hold.CreatedAt.Should().BeOnOrAfter(before);
        hold.ExpiresAt.Should().BeCloseTo(hold.CreatedAt.AddMinutes(15), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_EmptyItems_ThrowsDomainException()
        => FluentActions.Invoking(() => Hold.Create(null, [], 15))
            .Should().Throw<DomainException>();

    [Fact]
    public void Create_NullCustomerName_IsAllowed()
        => Hold.Create(null, OneItem(), 15).CustomerName.Should().BeNull();

    [Fact]
    public void MarkReleased_ActiveHold_SetsReleasedStatus_And_ReleasedAt()
    {
        var hold = Hold.Create(null, OneItem(), 15);
        hold.MarkReleased();

        hold.Status.Should().Be(HoldStatus.Released);
        hold.ReleasedAt.Should().NotBeNull();
        hold.ExpiredAt.Should().BeNull();
    }

    [Fact]
    public void MarkReleased_CalledTwice_ThrowsHoldTerminatedException()
    {
        var hold = Hold.Create(null, OneItem(), 15);
        hold.MarkReleased();

        FluentActions.Invoking(() => hold.MarkReleased())
            .Should().Throw<HoldTerminatedException>();
    }

    [Fact]
    public void MarkExpired_ActiveHold_SetsExpiredStatus_And_ExpiredAt()
    {
        var hold = Hold.Create(null, OneItem(), 15);
        hold.MarkExpired();

        hold.Status.Should().Be(HoldStatus.Expired);
        hold.ExpiredAt.Should().NotBeNull();
        hold.ReleasedAt.Should().BeNull();
    }

    [Fact]
    public void MarkExpired_CalledTwice_ThrowsHoldTerminatedException()
    {
        var hold = Hold.Create(null, OneItem(), 15);
        hold.MarkExpired();

        FluentActions.Invoking(() => hold.MarkExpired())
            .Should().Throw<HoldTerminatedException>();
    }

    [Fact]
    public void MarkReleased_OnExpiredHold_ThrowsHoldTerminatedException()
    {
        var hold = Hold.Create(null, OneItem(), 15);
        hold.MarkExpired();

        FluentActions.Invoking(() => hold.MarkReleased())
            .Should().Throw<HoldTerminatedException>()
            .Which.Status.Should().Be(HoldStatus.Expired);
    }
}
