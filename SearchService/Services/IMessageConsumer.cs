namespace SearchService.Services;

public interface IMessageConsumer : IDisposable
{
    void StartConsuming();
    void StopConsuming();
}
