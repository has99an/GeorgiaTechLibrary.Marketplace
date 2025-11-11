namespace NotificationService.Services;

public interface IMessageConsumer
{
    void StartConsuming();
    void StopConsuming();
}
