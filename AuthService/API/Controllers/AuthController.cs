using AuthService.Application.DTOs;
using AuthService.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.API.Controllers;

/// <summary>
/// Controller for authentication operations
/// </summary>
[ApiController]
[Route("")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new user
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(TokenDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<ActionResult<TokenDto>> Register([FromBody] RegisterDto registerDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var tokens = await _authService.RegisterAsync(registerDto);
        return Ok(tokens);
    }

    /// <summary>
    /// Authenticates a user and returns tokens
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<TokenDto>> Login([FromBody] LoginDto loginDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var tokens = await _authService.LoginAsync(loginDto);
        return Ok(tokens);
    }

    /// <summary>
    /// Refreshes an access token using a refresh token
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(TokenDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<ActionResult<TokenDto>> Refresh([FromBody] RefreshTokenDto refreshTokenDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var tokens = await _authService.RefreshTokenAsync(refreshTokenDto.RefreshToken);
        return Ok(tokens);
    }

    /// <summary>
    /// Validates a JWT token
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public IActionResult Validate([FromBody] ValidateTokenDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var isValid = _authService.ValidateToken(request.Token);
        
        if (isValid)
        {
            return Ok(new { Valid = true });
        }

        return Unauthorized(new { Valid = false });
    }
}

