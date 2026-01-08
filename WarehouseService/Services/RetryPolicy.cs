using Microsoft.Extensions.Logging;

namespace WarehouseService.Services;

/// <summary>
/// Retry policy helper with exponential backoff (2^n seconds)
/// </summary>
public static class RetryPolicy
{
    /// <summary>
    /// Executes an operation with retry logic using exponential backoff
    /// </summary>
    /// <typeparam name="T">Return type of the operation</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
    /// <param name="logger">Optional logger for retry attempts</param>
    /// <returns>Result of the operation</returns>
    /// <exception cref="Exception">Throws the last exception if all retries fail</exception>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        int maxRetries = 3,
        ILogger? logger = null)
    {
        int attempt = 0;
        Exception? lastException = null;

        while (attempt < maxRetries)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                lastException = ex;
                attempt++;
                
                if (attempt >= maxRetries)
                {
                    logger?.LogError(ex, 
                        "Operation failed after {Attempt} attempts. No more retries.",
                        attempt);
                    throw;
                }
                
                var delay = (int)Math.Pow(2, attempt); // 2s, 4s, 8s
                logger?.LogWarning(ex, 
                    "Operation failed (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}s...",
                    attempt, maxRetries, delay);
                
                await Task.Delay(TimeSpan.FromSeconds(delay));
            }
        }
        
        // Should never reach here, but compiler requires it
        throw lastException ?? new InvalidOperationException("Operation failed but no exception was captured");
    }

    /// <summary>
    /// Executes a void operation with retry logic using exponential backoff
    /// </summary>
    /// <param name="operation">The operation to execute</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
    /// <param name="logger">Optional logger for retry attempts</param>
    /// <exception cref="Exception">Throws the last exception if all retries fail</exception>
    public static async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        int maxRetries = 3,
        ILogger? logger = null)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await operation();
            return true;
        }, maxRetries, logger);
    }
}

