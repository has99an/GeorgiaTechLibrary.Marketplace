using AuthService.DTOs;
using AuthService.Models;
using AuthService.Repositories;
using AuthService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AuthService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthUserRepository _authUserRepository;
    private readonly IMessageProducer _messageProducer;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthUserRepository authUserRepository,
        IMessageProducer messageProducer,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _authUserRepository = authUserRepository;
        _messageProducer = messageProducer;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<TokenDto>> Register([FromBody] RegisterDto registerDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var emailExists = await _authUserRepository.EmailExistsAsync(registerDto.Email);
            if (emailExists)
            {
                return Conflict($"User with email {registerDto.Email} already exists");
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);
            var userId = Guid.NewGuid(); // Generate new UserId

            var authUser = new AuthUser
            {
                UserId = userId,
                Email = registerDto.Email,
                PasswordHash = passwordHash,
                CreatedDate = DateTime.UtcNow
            };

            var createdAuthUser = await _authUserRepository.AddAuthUserAsync(authUser);

            // Send UserCreated event to UserService
            var userEvent = new UserEvent
            {
                UserId = userId,
                Email = registerDto.Email,
                Name = "", // Name not provided
                Role = UserRole.Student, // Default role
                CreatedDate = createdAuthUser.CreatedDate
            };
            _messageProducer.SendMessage(userEvent, "UserCreated");

            // Generate tokens
            var tokens = GenerateTokens(createdAuthUser);

            return Ok(tokens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<TokenDto>> Login([FromBody] LoginDto loginDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var authUser = await _authUserRepository.GetAuthUserByEmailAsync(loginDto.Email);
            if (authUser == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, authUser.PasswordHash))
            {
                return Unauthorized("Invalid email or password");
            }

            var tokens = GenerateTokens(authUser);
            return Ok(tokens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging in user");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<TokenDto>> Refresh([FromBody] RefreshTokenDto refreshTokenDto)
    {
        try
        {
            // For simplicity, assume refresh token is the same as access token or something
            // In real implementation, store refresh tokens separately
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "defaultkey");

            try
            {
                var principal = tokenHandler.ValidateToken(refreshTokenDto.RefreshToken, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                }, out var validatedToken);

                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                {
                    return Unauthorized("Invalid token");
                }

                var userId = Guid.Parse(userIdClaim.Value);
                var authUser = await _authUserRepository.GetAuthUserByIdAsync(userId);
                if (authUser == null)
                {
                    return Unauthorized("User not found");
                }

                var tokens = GenerateTokens(authUser);
                return Ok(tokens);
            }
            catch
            {
                return Unauthorized("Invalid token");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("validate")]
    public IActionResult Validate()
    {
        // Token validation is handled by JWT middleware
        return Ok(new { Valid = true });
    }

    private TokenDto GenerateTokens(AuthUser authUser)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? "defaultkey");
        var expires = DateTime.UtcNow.AddHours(1);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, authUser.UserId.ToString()),
                new Claim(ClaimTypes.Email, authUser.Email)
            }),
            Expires = expires,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var accessToken = tokenHandler.WriteToken(token);

        // For refresh token, using same token for simplicity
        var refreshToken = accessToken;

        return new TokenDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = (int)(expires - DateTime.UtcNow).TotalSeconds
        };
    }
}
