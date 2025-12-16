using FluentAssertions;
using OrderService.Domain.ValueObjects;
using Xunit;

namespace OrderService.Tests.Unit.Domain.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Money_Create_WithValidAmount_Should_CreateMoney()
    {
        // Arrange & Act
        var money = Money.Create(100.50m);

        // Assert
        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Money_Create_WithCustomCurrency_Should_UseCurrency()
    {
        // Arrange & Act
        var money = Money.Create(100.50m, "EUR");

        // Assert
        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Money_Create_WithNegativeAmount_Should_ThrowException()
    {
        // Arrange & Act
        var act = () => Money.Create(-10m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Amount cannot be negative*");
    }

    [Fact]
    public void Money_Zero_Should_CreateZeroAmount()
    {
        // Arrange & Act
        var money = Money.Zero();

        // Assert
        money.Amount.Should().Be(0m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Money_Add_WithSameCurrency_Should_AddAmounts()
    {
        // Arrange
        var money1 = Money.Create(100m);
        var money2 = Money.Create(50m);

        // Act
        var result = money1.Add(money2);

        // Assert
        result.Amount.Should().Be(150m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Money_Add_WithDifferentCurrencies_Should_ThrowException()
    {
        // Arrange
        var money1 = Money.Create(100m, "USD");
        var money2 = Money.Create(50m, "EUR");

        // Act
        var act = () => money1.Add(money2);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*different currencies*");
    }

    [Fact]
    public void Money_Subtract_WithSameCurrency_Should_SubtractAmounts()
    {
        // Arrange
        var money1 = Money.Create(100m);
        var money2 = Money.Create(30m);

        // Act
        var result = money1.Subtract(money2);

        // Assert
        result.Amount.Should().Be(70m);
    }

    [Fact]
    public void Money_Subtract_ResultingInNegative_Should_ThrowException()
    {
        // Arrange
        var money1 = Money.Create(50m);
        var money2 = Money.Create(100m);

        // Act
        var act = () => money1.Subtract(money2);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Result cannot be negative");
    }

    [Fact]
    public void Money_Multiply_WithPositiveQuantity_Should_MultiplyAmount()
    {
        // Arrange
        var money = Money.Create(25m);

        // Act
        var result = money.Multiply(3);

        // Assert
        result.Amount.Should().Be(75m);
    }

    [Fact]
    public void Money_Multiply_WithNegativeQuantity_Should_ThrowException()
    {
        // Arrange
        var money = Money.Create(25m);

        // Act
        var act = () => money.Multiply(-1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Quantity cannot be negative*");
    }

    [Fact]
    public void Money_Equals_WithSameAmountAndCurrency_Should_ReturnTrue()
    {
        // Arrange
        var money1 = Money.Create(100m, "USD");
        var money2 = Money.Create(100m, "USD");

        // Act & Assert
        money1.Equals(money2).Should().BeTrue();
        (money1 == money2).Should().BeTrue();
    }

    [Fact]
    public void Money_Equals_WithDifferentAmounts_Should_ReturnFalse()
    {
        // Arrange
        var money1 = Money.Create(100m);
        var money2 = Money.Create(200m);

        // Act & Assert
        money1.Equals(money2).Should().BeFalse();
        (money1 != money2).Should().BeTrue();
    }

    [Fact]
    public void Money_GreaterThan_WithLargerAmount_Should_ReturnTrue()
    {
        // Arrange
        var money1 = Money.Create(100m);
        var money2 = Money.Create(50m);

        // Act & Assert
        (money1 > money2).Should().BeTrue();
        (money1 >= money2).Should().BeTrue();
    }

    [Fact]
    public void Money_LessThan_WithSmallerAmount_Should_ReturnTrue()
    {
        // Arrange
        var money1 = Money.Create(50m);
        var money2 = Money.Create(100m);

        // Act & Assert
        (money1 < money2).Should().BeTrue();
        (money1 <= money2).Should().BeTrue();
    }
}


