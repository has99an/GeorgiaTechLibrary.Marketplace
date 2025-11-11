namespace AuthService.Services;

public interface IMessageConsumer
{
    void StartConsuming();
    void StopConsuming();
}
