namespace ApiGateway.Configuration;

public class SecuritySettings
{
    public CorsSettings Cors { get; set; } = new();
    public RateLimitSettings RateLimit { get; set; } = new();
    public JwtSettings Jwt { get; set; } = new();
}

public class CorsSettings
{
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
    public bool AllowCredentials { get; set; } = true;
    public string[] AllowedMethods { get; set; } = new[] { "GET", "POST", "PUT", "DELETE", "PATCH" };
    public string[] AllowedHeaders { get; set; } = new[] { "*" };
}

public class RateLimitSettings
{
    public bool Enabled { get; set; } = true;
    public int GeneralLimit { get; set; } = 100;
    public int GeneralPeriodInSeconds { get; set; } = 60;
    public Dictionary<string, EndpointRateLimit> EndpointLimits { get; set; } = new();
}

public class EndpointRateLimit
{
    public int Limit { get; set; }
    public int PeriodInSeconds { get; set; }
}

public class JwtSettings
{
    public string AuthServiceUrl { get; set; } = "http://authservice:8080";
    public int ValidationCacheDurationMinutes { get; set; } = 5;
}

