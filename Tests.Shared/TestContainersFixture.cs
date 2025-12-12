using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

namespace Tests.Shared;

/// <summary>
/// Base fixture for Testcontainers setup - manages SQL Server, RabbitMQ, and Redis containers
/// </summary>
public class TestContainersFixture : IAsyncLifetime
{
    public MsSqlContainer? SqlServerContainer { get; private set; }
    public RabbitMqContainer? RabbitMqContainer { get; private set; }
    public RedisContainer? RedisContainer { get; private set; }

    public string SqlServerConnectionString => 
        SqlServerContainer?.GetConnectionString() ?? throw new InvalidOperationException("SQL Server container not initialized");

    public string RabbitMqConnectionString => 
        RabbitMqContainer?.GetConnectionString() ?? throw new InvalidOperationException("RabbitMQ container not initialized");

    public string RabbitMqHost => 
        RabbitMqContainer?.Hostname ?? throw new InvalidOperationException("RabbitMQ container not initialized");

    public int RabbitMqPort => 
        RabbitMqContainer?.GetMappedPublicPort(5672) ?? throw new InvalidOperationException("RabbitMQ container not initialized");

    public string RedisConnectionString => 
        RedisContainer?.GetConnectionString() ?? throw new InvalidOperationException("Redis container not initialized");

    public async Task InitializeAsync()
    {
        // Initialize SQL Server container
        SqlServerContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithPassword("YourStrong!Passw0rd")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .Build();

        // Initialize RabbitMQ container
        RabbitMqContainer = new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();

        // Initialize Redis container
        RedisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .Build();

        // Start all containers in parallel
        await Task.WhenAll(
            SqlServerContainer.StartAsync(),
            RabbitMqContainer.StartAsync(),
            RedisContainer.StartAsync()
        );
    }

    public async Task DisposeAsync()
    {
        var tasks = new List<Task>();

        if (SqlServerContainer != null)
            tasks.Add(SqlServerContainer.DisposeAsync().AsTask());
        
        if (RabbitMqContainer != null)
            tasks.Add(RabbitMqContainer.DisposeAsync().AsTask());
        
        if (RedisContainer != null)
            tasks.Add(RedisContainer.DisposeAsync().AsTask());

        await Task.WhenAll(tasks);
    }
}

