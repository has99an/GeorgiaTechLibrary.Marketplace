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
        _logger.LogInformation("=== USER REGISTRATION STARTED ===");
        _logger.LogInformation("Registration request received for email: {Email}", registerDto.Email);
        
        // Validate email
        _logger.LogInformation("Step 1: Validating email format...");
        var email = Email.Create(registerDto.Email);
        _logger.LogInformation("Step 1: Email validation successful: {Email}", email.Value);

        // Check if email already exists
        _logger.LogInformation("Step 2: Checking if email already exists...");
        var emailExists = await _authUserRepository.EmailExistsAsync(email.Value, cancellationToken);
        if (emailExists)
        {
            _logger.LogWarning("Step 2: Email already exists: {Email}", email.Value);
            throw new DuplicateEmailException(email.Value);
        }
        _logger.LogInformation("Step 2: Email does not exist, proceeding...");

        // Validate password
        _logger.LogInformation("Step 3: Validating password...");
        var password = Password.Create(registerDto.Password);
        _logger.LogInformation("Step 3: Password validation successful");

        // Hash password
        _logger.LogInformation("Step 4: Hashing password...");
        var passwordHash = _passwordHasher.HashPassword(password.Value);
        _logger.LogInformation("Step 4: Password hashed successfully");

        // Create auth user
        _logger.LogInformation("Step 5: Creating AuthUser entity...");
        var authUser = AuthUser.Create(email.Value, passwordHash);
        _logger.LogInformation("Step 5: AuthUser entity created with UserId: {UserId}", authUser.UserId);
        
        _logger.LogInformation("Step 6: Saving AuthUser to database...");
        var createdAuthUser = await _authUserRepository.AddAuthUserAsync(authUser, cancellationToken);
        _logger.LogInformation("Step 6: AuthUser saved to database. UserId: {UserId}, Email: {Email}", 
            createdAuthUser.UserId, createdAuthUser.GetMaskedEmail());

        _logger.LogInformation("=== USER REGISTERED IN AUTHSERVICE ===");
        _logger.LogInformation("UserId: {UserId}, Email: {Email}", 
            createdAuthUser.UserId, createdAuthUser.GetMaskedEmail());

        // Publish UserCreated event
        _logger.LogInformation("Step 7: Publishing UserCreated event...");
        PublishUserCreatedEvent(createdAuthUser, registerDto.Name);
        _logger.LogInformation("Step 7: UserCreated event publishing completed");

        // Generate tokens
        _logger.LogInformation("Step 8: Generating JWT tokens...");
        var tokens = _tokenService.GenerateTokens(createdAuthUser);
        _logger.LogInformation("Step 8: JWT tokens generated successfully");

        _logger.LogInformation("=== USER REGISTRATION COMPLETED ===");
        _logger.LogInformation("Final UserId: {UserId}, Email: {Email}", 
            createdAuthUser.UserId, createdAuthUser.GetMaskedEmail());

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

    private void PublishUserCreatedEvent(AuthUser authUser, string name)
    {
        _logger.LogInformation("=== PUBLISHING USERCREATED EVENT ===");
        _logger.LogInformation("AuthUser details - UserId: {UserId}, Email: {Email}, CreatedDate: {CreatedDate}", 
            authUser.UserId, authUser.GetMaskedEmail(), authUser.CreatedDate);
        
        try
        {
            _logger.LogInformation("Step 7.1: Creating UserEventDto...");
            var userEvent = new UserEventDto
            {
                UserId = authUser.UserId,
                Email = authUser.GetEmailString(),
                Name = name,
                Role = "Student", // Default role
                CreatedDate = authUser.CreatedDate
            };
            _logger.LogInformation("Step 7.1: UserEventDto created - UserId: {UserId}, Email: {Email}, Role: {Role}, CreatedDate: {CreatedDate}",
                userEvent.UserId, userEvent.Email, userEvent.Role, userEvent.CreatedDate);

            _logger.LogInformation("Step 7.2: Calling messageProducer.SendMessage with routing key 'UserCreated'...");
            _messageProducer.SendMessage(userEvent, "UserCreated");
            
            _logger.LogInformation("Step 7.2: messageProducer.SendMessage returned successfully");
            _logger.LogInformation("=== USERCREATED EVENT PUBLISHED SUCCESSFULLY ===");
            _logger.LogInformation("Event published for UserId: {UserId}, Email: {Email}", 
                authUser.UserId, authUser.GetMaskedEmail());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "=== FAILED TO PUBLISH USERCREATED EVENT ===");
            _logger.LogError(ex, "Exception details - UserId: {UserId}, Email: {Email}, ExceptionType: {ExceptionType}, Message: {Message}, StackTrace: {StackTrace}",
                authUser.UserId, authUser.GetMaskedEmail(), ex.GetType().Name, ex.Message, ex.StackTrace);
            // Don't throw - event publishing failure shouldn't fail registration
        }
    }
}

