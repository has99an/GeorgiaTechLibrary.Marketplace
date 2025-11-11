namespace OrderService.Services;

public interface IMessageConsumer
{
    void StartConsuming();
    void StopConsuming();
}
