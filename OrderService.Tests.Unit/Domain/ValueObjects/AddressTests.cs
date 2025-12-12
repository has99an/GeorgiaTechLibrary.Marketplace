using FluentAssertions;
using OrderService.Domain.ValueObjects;
using Xunit;

namespace OrderService.Tests.Unit.Domain.ValueObjects;

public class AddressTests
{
    [Fact]
    public void Address_Create_WithValidData_Should_CreateAddress()
    {
        // Arrange & Act
        var address = Address.Create("123 Main St", "Atlanta", "3033", "GA", "USA");

        // Assert
        address.Street.Should().Be("123 Main St");
        address.City.Should().Be("Atlanta");
        address.PostalCode.Should().Be("3033");
        address.State.Should().Be("GA");
        address.Country.Should().Be("USA");
    }

    [Fact]
    public void Address_Create_WithMinimalData_Should_UseDefaultCountry()
    {
        // Arrange & Act
        var address = Address.Create("123 Main St", "Copenhagen", "2100");

        // Assert
        address.Street.Should().Be("123 Main St");
        address.City.Should().Be("Copenhagen");
        address.PostalCode.Should().Be("2100");
        address.Country.Should().Be("Denmark");
    }

    [Fact]
    public void Address_Create_WithWhitespace_Should_TrimWhitespace()
    {
        // Arrange & Act
        var address = Address.Create("  123 Main St  ", "  Atlanta  ", "  3033  ");

        // Assert
        address.Street.Should().Be("123 Main St");
        address.City.Should().Be("Atlanta");
        address.PostalCode.Should().Be("3033");
    }

    [Fact]
    public void Address_Create_WithEmptyStreet_Should_ThrowException()
    {
        // Arrange & Act
        var act = () => Address.Create("", "Atlanta", "3033");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Street address is required*");
    }

    [Fact]
    public void Address_Create_WithEmptyCity_Should_ThrowException()
    {
        // Arrange & Act
        var act = () => Address.Create("123 Main St", "", "3033");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("City is required*");
    }

    [Fact]
    public void Address_Create_WithInvalidPostalCode_Should_ThrowException()
    {
        // Arrange & Act
        var act = () => Address.Create("123 Main St", "Atlanta", "ABC3");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Postal code must be 4 digits*");
    }

    [Fact]
    public void Address_Create_WithTooLongStreet_Should_ThrowException()
    {
        // Arrange
        var longStreet = new string('a', 201);

        // Act
        var act = () => Address.Create(longStreet, "Atlanta", "3033");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Street address cannot exceed 200 characters*");
    }

    [Fact]
    public void Address_GetFullAddress_Should_ReturnFormattedAddress()
    {
        // Arrange
        var address = Address.Create("123 Main St", "Atlanta", "3033", "GA", "USA");

        // Act
        var fullAddress = address.GetFullAddress();

        // Assert
        fullAddress.Should().Contain("123 Main St");
        fullAddress.Should().Contain("3033");
        fullAddress.Should().Contain("Atlanta");
        fullAddress.Should().Contain("GA");
        fullAddress.Should().Contain("USA");
    }

    [Fact]
    public void Address_Equals_WithSameValues_Should_ReturnTrue()
    {
        // Arrange
        var address1 = Address.Create("123 Main St", "Atlanta", "3033", "GA", "USA");
        var address2 = Address.Create("123 Main St", "Atlanta", "3033", "GA", "USA");

        // Act & Assert
        address1.Equals(address2).Should().BeTrue();
        (address1 == address2).Should().BeTrue();
    }

    [Fact]
    public void Address_Equals_WithDifferentValues_Should_ReturnFalse()
    {
        // Arrange
        var address1 = Address.Create("123 Main St", "Atlanta", "3033");
        var address2 = Address.Create("456 Oak Ave", "Atlanta", "3033");

        // Act & Assert
        address1.Equals(address2).Should().BeFalse();
        (address1 != address2).Should().BeTrue();
    }
}

