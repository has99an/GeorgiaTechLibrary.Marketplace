using AuthService.Application.DTOs;
using AuthService.Application.Interfaces;
using AuthService.Application.Services;
using AuthService.Domain.Entities;
using AuthService.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AuthService.Tests.Integration.Application.Services;

public class AuthServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IMessageProducer> _messageProducerMock;
    private readonly Mock<ILogger<AuthService.Application.Services.AuthService>> _loggerMock;
    private readonly AuthService.Application.Services.AuthService _authService;

    public AuthServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
        
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _tokenServiceMock = new Mock<ITokenService>();
        _messageProducerMock = new Mock<IMessageProducer>();
        _loggerMock = new Mock<ILogger<AuthService.Application.Services.AuthService>>();

        var repositoryLogger = new Mock<ILogger<AuthService.Infrastructure.Persistence.AuthUserRepository>>();
        _authService = new AuthService.Application.Services.AuthService(
            new AuthService.Infrastructure.Persistence.AuthUserRepository(_context, repositoryLogger.Object),
            _tokenServiceMock.Object,
            _passwordHasherMock.Object,
            _messageProducerMock.Object,
            _loggerMock.Object
        );
    }

    [Fact]
    public async Task AuthService_RegisterAsync_WithValidData_Should_CreateUser()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = "newuser@gatech.edu",
            Password = "SecurePass123!",
            Name = "Test User"
        };

        _passwordHasherMock.Setup(x => x.HashPassword(It.IsAny<string>()))
            .Returns("hashedpassword");

        _tokenServiceMock.Setup(x => x.GenerateTokens(It.IsAny<AuthUser>(), It.IsAny<string>()))
            .Returns(new TokenDto { AccessToken = "token", RefreshToken = "refresh" });

        // Act
        var result = await _authService.RegisterAsync(registerDto);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeEmpty();
        
        var userInDb = await _context.AuthUsers.FirstOrDefaultAsync(u => u.GetEmailString() == registerDto.Email);
        userInDb.Should().NotBeNull();
        
        _messageProducerMock.Verify(x => x.SendMessage(It.IsAny<object>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task AuthService_LoginAsync_WithValidCredentials_Should_ReturnTokens()
    {
        // Arrange
        var email = "test@gatech.edu";
        var password = "password123";
        var hashedPassword = "hashedpassword";

        var user = AuthUser.Create(email, hashedPassword, "Student");
        _context.AuthUsers.Add(user);
        await _context.SaveChangesAsync();

        _passwordHasherMock.Setup(x => x.VerifyPassword(password, hashedPassword))
            .Returns(true);

        _tokenServiceMock.Setup(x => x.GenerateTokens(It.IsAny<AuthUser>(), It.IsAny<string>()))
            .Returns(new TokenDto { AccessToken = "token", RefreshToken = "refresh" });

        var loginDto = new LoginDto { Email = email, Password = password };

        // Act
        var result = await _authService.LoginAsync(loginDto);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeEmpty();
        result.RefreshToken.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AuthService_LoginAsync_WithInvalidCredentials_Should_ThrowException()
    {
        // Arrange
        var email = "test@gatech.edu";
        var password = "wrongpassword";
        var hashedPassword = "hashedpassword";

        var user = AuthUser.Create(email, hashedPassword, "Student");
        _context.AuthUsers.Add(user);
        await _context.SaveChangesAsync();

        _passwordHasherMock.Setup(x => x.VerifyPassword(password, hashedPassword))
            .Returns(false);

        var loginDto = new LoginDto { Email = email, Password = password };

        // Act
        var act = async () => await _authService.LoginAsync(loginDto);

        // Assert
        await act.Should().ThrowAsync<AuthService.Domain.Exceptions.InvalidCredentialsException>();
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}

