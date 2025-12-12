using AuthService.Domain.Exceptions;
using AuthService.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace AuthService.Tests.Unit.Domain.ValueObjects;

public class EmailTests
{
    [Fact]
    public void Email_Create_WithValidEmail_Should_CreateEmail()
    {
        // Arrange & Act
        var email = Email.Create("test@gatech.edu");

        // Assert
        email.Value.Should().Be("test@gatech.edu");
    }

    [Fact]
    public void Email_Create_WithUppercaseEmail_Should_ConvertToLowercase()
    {
        // Arrange & Act
        var email = Email.Create("TEST@GATECH.EDU");

        // Assert
        email.Value.Should().Be("test@gatech.edu");
    }

    [Fact]
    public void Email_Create_WithWhitespace_Should_TrimWhitespace()
    {
        // Arrange & Act
        var email = Email.Create("  test@gatech.edu  ");

        // Assert
        email.Value.Should().Be("test@gatech.edu");
    }

    [Fact]
    public void Email_Create_WithEmptyString_Should_ThrowException()
    {
        // Arrange & Act
        var act = () => Email.Create("");

        // Assert
        act.Should().Throw<AuthenticationException>()
            .WithMessage("Email address is required");
    }

    [Fact]
    public void Email_Create_WithNull_Should_ThrowException()
    {
        // Arrange & Act
        var act = () => Email.Create(null!);

        // Assert
        act.Should().Throw<AuthenticationException>()
            .WithMessage("Email address is required");
    }

    [Fact]
    public void Email_Create_WithInvalidFormat_Should_ThrowException()
    {
        // Arrange & Act
        var act = () => Email.Create("notanemail");

        // Assert
        act.Should().Throw<AuthenticationException>()
            .WithMessage("Email address format is invalid");
    }

    [Fact]
    public void Email_Create_WithTooLongEmail_Should_ThrowException()
    {
        // Arrange
        var longEmail = new string('a', 250) + "@gatech.edu";

        // Act
        var act = () => Email.Create(longEmail);

        // Assert
        act.Should().Throw<AuthenticationException>()
            .WithMessage("Email address cannot exceed 255 characters");
    }

    [Fact]
    public void Email_GetMaskedValue_WithShortLocalPart_Should_MaskAll()
    {
        // Arrange
        var email = Email.Create("ab@gatech.edu");

        // Act
        var masked = email.GetMaskedValue();

        // Assert
        masked.Should().Be("**@gatech.edu");
    }

    [Fact]
    public void Email_GetMaskedValue_WithLongLocalPart_Should_MaskPartially()
    {
        // Arrange
        var email = Email.Create("testuser@gatech.edu");

        // Act
        var masked = email.GetMaskedValue();

        // Assert
        masked.Should().Be("tes***@gatech.edu");
    }

    [Fact]
    public void Email_Equals_WithSameValue_Should_ReturnTrue()
    {
        // Arrange
        var email1 = Email.Create("test@gatech.edu");
        var email2 = Email.Create("test@gatech.edu");

        // Act & Assert
        email1.Equals(email2).Should().BeTrue();
        email1.Value.Should().Be(email2.Value);
        email1.GetHashCode().Should().Be(email2.GetHashCode());
    }

    [Fact]
    public void Email_Create_NormalizesToLowercase()
    {
        // Arrange & Act
        var email1 = Email.Create("TEST@GATECH.EDU");
        var email2 = Email.Create("test@gatech.edu");

        // Assert
        email1.Value.Should().Be("test@gatech.edu");
        email2.Value.Should().Be("test@gatech.edu");
        email1.Value.Should().Be(email2.Value);
    }

    [Fact]
    public void Email_Equals_WithDifferentValue_Should_ReturnFalse()
    {
        // Arrange
        var email1 = Email.Create("test1@gatech.edu");
        var email2 = Email.Create("test2@gatech.edu");

        // Act & Assert
        email1.Equals(email2).Should().BeFalse();
    }

    [Fact]
    public void Email_ToString_Should_ReturnValue()
    {
        // Arrange
        var email = Email.Create("test@gatech.edu");

        // Act & Assert
        email.ToString().Should().Be("test@gatech.edu");
    }

    [Fact]
    public void Email_ImplicitStringConversion_Should_ReturnValue()
    {
        // Arrange
        var email = Email.Create("test@gatech.edu");

        // Act
        string emailString = email;

        // Assert
        emailString.Should().Be("test@gatech.edu");
    }
}

