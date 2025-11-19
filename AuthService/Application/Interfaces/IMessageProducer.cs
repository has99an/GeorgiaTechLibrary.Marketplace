namespace AuthService.Application.Interfaces;

/// <summary>
/// Interface for publishing messages to message broker
/// </summary>
public interface IMessageProducer
{
    /// <summary>
    /// Sends a message to the message broker
    /// </summary>
    void SendMessage<T>(T message, string routingKey);
}

