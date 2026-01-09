using Tests.Shared;
using Xunit;

namespace SAGACompensation.Tests.Integration;

/// <summary>
/// Fixture for SAGA compensation integration tests
/// Uses existing docker-compose services for fast test execution
/// </summary>
public class SAGACompensationFixture : IClassFixture<LocalServicesFixture>, IDisposable
{
    protected readonly LocalServicesFixture Services;
    private RabbitMQTestHelper? _rabbitMQHelper;
    protected RabbitMQTestHelper RabbitMQHelper
    {
        get
        {
            // Recreate helper if it was disposed or channel is closed
            if (_rabbitMQHelper == null)
            {
                Console.WriteLine("[SAGACompensationFixture] Creating new RabbitMQTestHelper");
                _rabbitMQHelper = new RabbitMQTestHelper(Services.RabbitMqConnectionString);
                
                // Declare exchanges
                _rabbitMQHelper.DeclareExchange("book_events");
                _rabbitMQHelper.DeclareExchange("book_events.dlq");
                _rabbitMQHelper.DeclareExchange("order_events");
                _rabbitMQHelper.DeclareExchange("order_events.dlq");
                
                Console.WriteLine($"[SAGACompensationFixture] Using pre-created test queues: {CompensationRequiredQueue}, {OrderCancellationQueue}");
            }
            return _rabbitMQHelper;
        }
    }
    private readonly List<string> _testDatabases = new();
    private readonly List<string> _testQueues = new();
    
    // Persistent test queues shared across all tests
    // These queues are pre-created in RabbitMQ and reused across all test runs
    public string CompensationRequiredQueue => "saga_test_compensation_required";
    public string OrderCancellationQueue => "saga_test_order_cancellation";

    public SAGACompensationFixture(LocalServicesFixture services)
    {
        Services = services;
        // Don't create RabbitMQHelper here - let it be created lazily on first access
        // This ensures the helper is created fresh for each test class
    }

    /// <summary>
    /// Creates a unique test database name for isolation
    /// </summary>
    public string CreateTestDatabaseName(string prefix = "Test")
    {
        var dbName = $"{prefix}_{Guid.NewGuid():N}";
        _testDatabases.Add(dbName);
        return dbName;
    }

    /// <summary>
    /// Registers a test queue for cleanup
    /// </summary>
    public void RegisterTestQueue(string queueName)
    {
        _testQueues.Add(queueName);
    }

    /// <summary>
    /// Cleanup test data after each test
    /// </summary>
    public void Dispose()
    {
        // Purge test queues
        foreach (var queue in _testQueues)
        {
            try
            {
                RabbitMQHelper.PurgeQueue(queue);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        // Purge all queues created by helper
        try
        {
            RabbitMQHelper.PurgeQueues();
        }
        catch
        {
            // Ignore cleanup errors
        }

        // Note: Test databases are not automatically deleted
        // They will be cleaned up manually or on next docker-compose down
        
        // NOTE: Do NOT dispose RabbitMQHelper here!
        // The helper's channel gets closed, causing subsequent tests to fail
        // Let the GC handle cleanup or dispose in a finalizer
        // RabbitMQHelper?.Dispose();
    }
}

