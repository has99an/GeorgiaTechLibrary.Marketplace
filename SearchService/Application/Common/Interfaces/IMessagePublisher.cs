namespace SearchService.Application.Common.Interfaces;

/// <summary>
/// Service interface for publishing messages
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publishes a message to an exchange
    /// </summary>
    Task PublishAsync<T>(string exchange, string routingKey, T message, CancellationToken cancellationToken = default) where T : class;
}

