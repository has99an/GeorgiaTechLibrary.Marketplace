using RabbitMQ.Client;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Tests.Shared;

/// <summary>
/// Fixture for using existing docker-compose services (localhost)
/// Much faster than Testcontainers - assumes services are already running
/// </summary>
public class LocalServicesFixture : IAsyncLifetime
{
    public string SqlServerConnectionString { get; private set; } = string.Empty;
    public string RabbitMqConnectionString { get; private set; } = string.Empty;
    public string RabbitMqHost { get; private set; } = "localhost";
    public int RabbitMqPort { get; private set; } = 5672;
    public string RedisConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // Use existing docker-compose services
        // Use master database for health check - it always exists
        SqlServerConnectionString = "Server=localhost;Database=master;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=true;Connection Timeout=10;";
        RabbitMqConnectionString = "amqp://guest:guest@localhost:5672/";
        RedisConnectionString = "localhost:6379";

        // Quick health check - verify services are accessible
        await VerifyServicesAsync();
    }

    private async Task VerifyServicesAsync()
    {
        // Note: SQL Server health check removed - tests only use RabbitMQ
        // SQL Server connection string is provided but not validated here
        // as it may only be accessible from container network
        
        // Quick connection test to RabbitMQ (with timeout)
        try
        {
            var factory = new RabbitMQ.Client.ConnectionFactory
            {
                HostName = RabbitMqHost,
                Port = RabbitMqPort,
                UserName = "guest",
                Password = "guest",
                RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
            };
            using var connection = factory.CreateConnection();
            connection.Close();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"RabbitMQ is not accessible at localhost:5672. " +
                $"Make sure docker-compose services are running. Error: {ex.Message}");
        }
    }

    public async Task DisposeAsync()
    {
        // No cleanup needed - using existing services
        await Task.CompletedTask;
    }
}

