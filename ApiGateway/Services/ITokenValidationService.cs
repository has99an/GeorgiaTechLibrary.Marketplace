namespace ApiGateway.Services;

public interface ITokenValidationService
{
    Task<bool> ValidateTokenAsync(string token);
    string? ExtractUserIdFromToken(string token);
}

