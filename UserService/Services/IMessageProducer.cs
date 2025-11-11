namespace UserService.Services;

public interface IMessageProducer
{
    void SendMessage<T>(T message, string routingKey);
}
