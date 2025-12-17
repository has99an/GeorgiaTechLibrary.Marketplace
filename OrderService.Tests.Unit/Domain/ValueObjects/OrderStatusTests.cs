using FluentAssertions;
using OrderService.Domain.ValueObjects;
using Xunit;

namespace OrderService.Tests.Unit.Domain.ValueObjects;

public class OrderStatusTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Paid, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Paid, OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Paid, OrderStatus.Refunded, true)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Paid, false)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Refunded, true)]
    public void OrderStatus_CanTransitionTo_WithValidTransitions_Should_ReturnExpected(
        OrderStatus current, OrderStatus target, bool expected)
    {
        // Act
        var result = current.CanTransitionTo(target);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void OrderStatus_Cancelled_Should_NotAllowTransitions()
    {
        // Arrange
        var status = OrderStatus.Cancelled;

        // Act & Assert
        status.CanTransitionTo(OrderStatus.Paid).Should().BeFalse();
        status.CanTransitionTo(OrderStatus.Shipped).Should().BeFalse();
    }

    [Fact]
    public void OrderStatus_Refunded_Should_NotAllowTransitions()
    {
        // Arrange
        var status = OrderStatus.Refunded;

        // Act & Assert
        status.CanTransitionTo(OrderStatus.Paid).Should().BeFalse();
        status.CanTransitionTo(OrderStatus.Shipped).Should().BeFalse();
    }

    [Theory]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Paid, true)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Refunded, false)]
    public void OrderStatus_CanBeCancelled_Should_ReturnExpected(OrderStatus status, bool expected)
    {
        // Act
        var result = status.CanBeCancelled();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(OrderStatus.Paid, true)]
    [InlineData(OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Refunded, false)]
    public void OrderStatus_CanBeRefunded_Should_ReturnExpected(OrderStatus status, bool expected)
    {
        // Act
        var result = status.CanBeRefunded();

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Refunded, true)]
    [InlineData(OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Paid, false)]
    [InlineData(OrderStatus.Shipped, false)]
    public void OrderStatus_IsTerminal_Should_ReturnExpected(OrderStatus status, bool expected)
    {
        // Act
        var result = status.IsTerminal();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void OrderStatus_ToDisplayString_Should_ReturnFormattedString()
    {
        // Act & Assert
        OrderStatus.Pending.ToDisplayString().Should().Be("Pending Payment");
        OrderStatus.Paid.ToDisplayString().Should().Be("Paid");
        OrderStatus.Shipped.ToDisplayString().Should().Be("Shipped");
        OrderStatus.Delivered.ToDisplayString().Should().Be("Delivered");
        OrderStatus.Cancelled.ToDisplayString().Should().Be("Cancelled");
        OrderStatus.Refunded.ToDisplayString().Should().Be("Refunded");
    }
}



