namespace ApiGateway.Services;

public interface ISwaggerAggregationService
{
    Task<string?> GetSwaggerDocumentAsync(string serviceName);
    IEnumerable<string> GetAvailableServices();
}

