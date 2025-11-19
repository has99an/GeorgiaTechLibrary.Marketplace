using AuthService.Application.DTOs;
using AuthService.Application.Interfaces;
using AuthService.Domain.Entities;
using AuthService.Domain.Exceptions;
using AuthService.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AuthService.Application.Services;

/// <summary>
/// Service implementation for authentication business logic
/// </summary>
public class AuthService : IAuthService
{
    private readonly IAuthUserRepository _authUserRepository;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMessageProducer _messageProducer;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IAuthUserRepository authUserRepository,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        IMessageProducer messageProducer,
        ILogger<AuthService> logger)
    {
        _authUserRepository = authUserRepository;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _messageProducer = messageProducer;
        _logger = logger;
    }

    public async Task<TokenDto> RegisterAsync(RegisterDto registerDto, CancellationToken cancellationToken = default)
    {
        // Validate email
        var email = Email.Create(registerDto.Email);

        // Check if email already exists
        var emailExists = await _authUserRepository.EmailExistsAsync(email.Value, cancellationToken);
        if (emailExists)
        {
            throw new DuplicateEmailException(email.Value);
        }

        // Validate password
        var password = Password.Create(registerDto.Password);

        // Hash password
        var passwordHash = _passwordHasher.HashPassword(password.Value);

        // Create auth user
        var authUser = AuthUser.Create(email.Value, passwordHash);
        var createdAuthUser = await _authUserRepository.AddAuthUserAsync(authUser, cancellationToken);

        _logger.LogInformation("User registered: {UserId}, Email: {Email}", 
            createdAuthUser.UserId, createdAuthUser.GetMaskedEmail());

        // Publish UserCreated event
        PublishUserCreatedEvent(createdAuthUser);

        // Generate tokens
        var tokens = _tokenService.GenerateTokens(createdAuthUser);

        return tokens;
    }

    public async Task<TokenDto> LoginAsync(LoginDto loginDto, CancellationToken cancellationToken = default)
    {
        // Validate email format
        var email = Email.Create(loginDto.Email);

        // Get user by email
        var authUser = await _authUserRepository.GetAuthUserByEmailAsync(email.Value, cancellationToken);
        
        if (authUser == null)
        {
            _logger.LogWarning("Login attempt for non-existent email: {Email}", email.GetMaskedValue());
            throw new InvalidCredentialsException(email.Value);
        }

        // Check if account is locked out
        if (authUser.IsLockedOut())
        {
            _logger.LogWarning("Login attempt for locked account: {UserId}", authUser.UserId);
            throw new AuthenticationException("Account is temporarily locked due to multiple failed login attempts");
        }

        // Verify password
        if (!_passwordHasher.VerifyPassword(loginDto.Password, authUser.PasswordHash))
        {
            authUser.RecordFailedLogin();
            await _authUserRepository.UpdateAuthUserAsync(authUser, cancellationToken);
            
            _logger.LogWarning("Failed login attempt for user: {UserId}, Attempts: {Attempts}", 
                authUser.UserId, authUser.FailedLoginAttempts);
            
            throw new InvalidCredentialsException(email.Value);
        }

        // Record successful login
        authUser.RecordSuccessfulLogin();
        await _authUserRepository.UpdateAuthUserAsync(authUser, cancellationToken);

        _logger.LogInformation("User logged in successfully: {UserId}", authUser.UserId);

        // Generate tokens
        var tokens = _tokenService.GenerateTokens(authUser);

        return tokens;
    }

    public async Task<TokenDto> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        // Validate refresh token
        if (!_tokenService.ValidateToken(refreshToken))
        {
            throw new AuthenticationException("Invalid refresh token");
        }

        // Extract user ID from token
        var userId = _tokenService.ExtractUserIdFromToken(refreshToken);
        if (userId == null)
        {
            throw new AuthenticationException("Invalid token: User ID not found");
        }

        // Get user
        var authUser = await _authUserRepository.GetAuthUserByIdAsync(userId.Value, cancellationToken);
        if (authUser == null)
        {
            throw new AuthenticationException("User not found");
        }

        _logger.LogInformation("Token refreshed for user: {UserId}", authUser.UserId);

        // Generate new tokens
        var tokens = _tokenService.GenerateTokens(authUser);

        return tokens;
    }

    public bool ValidateToken(string token)
    {
        return _tokenService.ValidateToken(token);
    }

    private void PublishUserCreatedEvent(AuthUser authUser)
    {
        try
        {
            var userEvent = new UserEventDto
            {
                UserId = authUser.UserId,
                Email = authUser.GetEmailString(),
                Name = string.Empty, // Name not provided during registration
                Role = "Student", // Default role
                CreatedDate = authUser.CreatedDate
            };

            _messageProducer.SendMessage(userEvent, "UserCreated");
            
            _logger.LogInformation("UserCreated event published for UserId: {UserId}", authUser.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish UserCreated event for UserId: {UserId}", authUser.UserId);
            // Don't throw - event publishing failure shouldn't fail registration
        }
    }
}

