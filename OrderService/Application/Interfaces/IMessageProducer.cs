namespace OrderService.Application.Interfaces;

/// <summary>
/// Interface for publishing messages to message broker
/// </summary>
public interface IMessageProducer
{
    void SendMessage<T>(T message, string routingKey);
    Task SendMessageAsync<T>(T message, string routingKey);
}

