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
    protected readonly RabbitMQTestHelper RabbitMQHelper;
    private readonly List<string> _testDatabases = new();
    private readonly List<string> _testQueues = new();

    public SAGACompensationFixture(LocalServicesFixture services)
    {
        Services = services;
        RabbitMQHelper = new RabbitMQTestHelper(Services.RabbitMqConnectionString);
        
        // Declare exchanges
        RabbitMQHelper.DeclareExchange("book_events");
        RabbitMQHelper.DeclareExchange("book_events.dlq");
        RabbitMQHelper.DeclareExchange("order_events");
        RabbitMQHelper.DeclareExchange("order_events.dlq");
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
        RabbitMQHelper?.Dispose();
    }
}

