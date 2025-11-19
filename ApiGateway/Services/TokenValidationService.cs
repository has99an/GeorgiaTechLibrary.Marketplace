using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ApiGateway.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ApiGateway.Services;

public class TokenValidationService : ITokenValidationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TokenValidationService> _logger;
    private readonly IMemoryCache _cache;
    private readonly JwtSettings _jwtSettings;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public TokenValidationService(
        HttpClient httpClient,
        ILogger<TokenValidationService> logger,
        IMemoryCache cache,
        IOptions<SecuritySettings> securitySettings)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cache = cache;
        _jwtSettings = securitySettings.Value.Jwt;
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        // Check cache first
        var cacheKey = $"token_validation_{token.GetHashCode()}";
        if (_cache.TryGetValue<bool>(cacheKey, out var cachedResult))
        {
            _logger.LogDebug("Token validation result retrieved from cache");
            return cachedResult;
        }

        try
        {
            var validateUrl = $"{_jwtSettings.AuthServiceUrl}/validate";

            var request = new HttpRequestMessage(HttpMethod.Post, validateUrl)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { Token = token }),
                    Encoding.UTF8,
                    "application/json")
            };

            var response = await _httpClient.SendAsync(request);
            var isValid = response.IsSuccessStatusCode;

            // Cache the result
            if (isValid)
            {
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(_jwtSettings.ValidationCacheDurationMinutes));
                _cache.Set(cacheKey, isValid, cacheOptions);
            }

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token with AuthService");
            return false;
        }
    }

    public string? ExtractUserIdFromToken(string token)
    {
        try
        {
            var jwtToken = _tokenHandler.ReadToken(token) as JwtSecurityToken;

            if (jwtToken == null)
            {
                _logger.LogWarning("Failed to read JWT token");
                return null;
            }

            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            return userIdClaim?.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting UserId from token");
            return null;
        }
    }
}

