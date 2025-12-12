using FluentAssertions;
using OrderService.Domain.Entities;
using OrderService.Domain.Exceptions;
using OrderService.Domain.ValueObjects;
using Xunit;

namespace OrderService.Tests.Unit.Domain.Entities;

public class OrderTests
{
    [Fact]
    public void Order_Create_WithValidData_Should_CreateOrder()
    {
        // Arrange
        var customerId = "customer-123";
        var orderItems = new List<OrderItem>
        {
            OrderItem.Create("9780123456789", "seller-1", 2, 29.99m)
        };
        var address = Address.Create("123 Main St", "Atlanta", "30332", "GA", "USA");

        // Act
        var order = Order.Create(customerId, orderItems, address);

        // Assert
        order.Should().NotBeNull();
        order.CustomerId.Should().Be(customerId);
        order.Status.Should().Be(OrderStatus.Pending);
        order.OrderItems.Count.Should().Be(1);
        order.TotalAmount.Amount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Order_Create_WithEmptyCustomerId_Should_ThrowException()
    {
        // Arrange
        var orderItems = new List<OrderItem>
        {
            OrderItem.Create("9780123456789", "seller-1", 1, 29.99m)
        };
        var address = Address.Create("123 Main St", "Atlanta", "30332");

        // Act
        var act = () => Order.Create("", orderItems, address);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Customer ID cannot be empty*");
    }

    [Fact]
    public void Order_Create_WithNoItems_Should_ThrowException()
    {
        // Arrange
        var address = Address.Create("123 Main St", "Atlanta", "30332");

        // Act
        var act = () => Order.Create("customer-123", new List<OrderItem>(), address);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Order must contain at least one item*");
    }

    [Fact]
    public void Order_Create_WithNullAddress_Should_ThrowException()
    {
        // Arrange
        var orderItems = new List<OrderItem>
        {
            OrderItem.Create("9780123456789", "seller-1", 1, 29.99m)
        };

        // Act
        var act = () => Order.Create("customer-123", orderItems, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*Delivery address is required*");
    }

    [Fact]
    public void Order_ProcessPayment_WithCorrectAmount_Should_UpdateStatus()
    {
        // Arrange
        var order = CreateTestOrder();
        var totalAmount = order.TotalAmount.Amount;

        // Act
        order.ProcessPayment(totalAmount);

        // Assert
        order.Status.Should().Be(OrderStatus.Paid);
        order.PaidDate.Should().NotBeNull();
    }

    [Fact]
    public void Order_ProcessPayment_WithIncorrectAmount_Should_ThrowException()
    {
        // Arrange
        var order = CreateTestOrder();
        var totalAmount = order.TotalAmount.Amount;

        // Act
        var act = () => order.ProcessPayment(totalAmount - 10m);

        // Assert
        act.Should().Throw<InvalidPaymentException>();
    }

    [Fact]
    public void Order_ProcessPayment_WhenNotPending_Should_ThrowException()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ProcessPayment(order.TotalAmount.Amount);
        var totalAmount = order.TotalAmount.Amount;

        // Act
        var act = () => order.ProcessPayment(totalAmount);

        // Assert
        act.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void Order_MarkAsShipped_FromPaidStatus_Should_UpdateStatus()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ProcessPayment(order.TotalAmount.Amount);

        // Act
        order.MarkAsShipped();

        // Assert
        order.Status.Should().Be(OrderStatus.Shipped);
        order.ShippedDate.Should().NotBeNull();
    }

    [Fact]
    public void Order_MarkAsShipped_FromPendingStatus_Should_ThrowException()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        var act = () => order.MarkAsShipped();

        // Assert
        act.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void Order_Cancel_FromPendingStatus_Should_UpdateStatus()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act
        order.Cancel("Customer requested cancellation");

        // Assert
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancelledDate.Should().NotBeNull();
        order.CancellationReason.Should().Be("Customer requested cancellation");
    }

    [Fact]
    public void Order_Cancel_FromShippedStatus_Should_ThrowException()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ProcessPayment(order.TotalAmount.Amount);
        order.MarkAsShipped();

        // Act
        var act = () => order.Cancel("Reason");

        // Assert
        act.Should().Throw<InvalidOrderStateException>();
    }

    [Fact]
    public void Order_ProcessRefund_FromPaidStatus_Should_UpdateStatus()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ProcessPayment(order.TotalAmount.Amount);

        // Act
        order.ProcessRefund("Defective product");

        // Assert
        order.Status.Should().Be(OrderStatus.Refunded);
        order.RefundedDate.Should().NotBeNull();
        order.RefundReason.Should().Be("Defective product");
    }

    [Fact]
    public void Order_GetSellerIds_Should_ReturnUniqueSellerIds()
    {
        // Arrange
        var orderItems = new List<OrderItem>
        {
            OrderItem.Create("9780123456789", "seller-1", 1, 29.99m),
            OrderItem.Create("9780123456790", "seller-1", 1, 19.99m),
            OrderItem.Create("9780123456791", "seller-2", 1, 39.99m)
        };
        var address = Address.Create("123 Main St", "Atlanta", "30332");
        var order = Order.Create("customer-123", orderItems, address);

        // Act
        var sellerIds = order.GetSellerIds().ToList();

        // Assert
        sellerIds.Should().HaveCount(2);
        sellerIds.Should().Contain("seller-1");
        sellerIds.Should().Contain("seller-2");
    }

    [Fact]
    public void Order_CanBeModified_WhenPending_Should_ReturnTrue()
    {
        // Arrange
        var order = CreateTestOrder();

        // Act & Assert
        order.CanBeModified().Should().BeTrue();
    }

    [Fact]
    public void Order_CanBeModified_WhenPaid_Should_ReturnFalse()
    {
        // Arrange
        var order = CreateTestOrder();
        order.ProcessPayment(order.TotalAmount.Amount);

        // Act & Assert
        order.CanBeModified().Should().BeFalse();
    }

    private static Order CreateTestOrder()
    {
        var orderItems = new List<OrderItem>
        {
            OrderItem.Create("9780123456789", "seller-1", 2, 29.99m)
        };
        var address = Address.Create("123 Main St", "Atlanta", "30332", "GA", "USA");
        return Order.Create("customer-123", orderItems, address);
    }
}

