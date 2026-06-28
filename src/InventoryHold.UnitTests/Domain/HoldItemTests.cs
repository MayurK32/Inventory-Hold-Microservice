using FluentAssertions;
using InventoryHold.Domain.Entities;
using InventoryHold.Domain.Exceptions;

namespace InventoryHold.UnitTests.Domain;

public class HoldItemTests
{
    [Fact]
    public void Constructor_ValidInput_CreatesItem()
    {
        var item = new HoldItem("widget-a", "Widget A", 3);
        item.ProductId.Should().Be("widget-a");
        item.ProductName.Should().Be("Widget A");
        item.Quantity.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_QuantityBelowOne_ThrowsDomainException(int qty)
        => FluentActions.Invoking(() => new HoldItem("widget-a", "Widget A", qty))
            .Should().Throw<DomainException>();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyProductId_ThrowsDomainException(string? productId)
        => FluentActions.Invoking(() => new HoldItem(productId!, "Widget A", 1))
            .Should().Throw<DomainException>();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyProductName_ThrowsDomainException(string productName)
        => FluentActions.Invoking(() => new HoldItem("widget-a", productName, 1))
            .Should().Throw<DomainException>();
}
