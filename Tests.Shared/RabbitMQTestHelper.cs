using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using RabbitMQ.Client;
using Tests.Shared;

namespace Tests.Shared;

/// <summary>
/// Helper class for RabbitMQ testing operations
/// </summary>
public class RabbitMQTestHelper : IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly List<string> _queues = new();
    private readonly List<string> _exchanges = new();

    public RabbitMQTestHelper(string connectionString)
    {
        try
        {
            // Match PRECISELY CompensationService's configuration
            // CompensationService uses ConnectionFactory with:
            // HostName = "localhost", Port = 5672, UserName = "guest", Password = "guest"
            // No VirtualHost specified (defaults to "/")
            var factory = new ConnectionFactory
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "guest",
                Password = "guest",
                // VirtualHost defaults to "/" - same as CompensationService
                AutomaticRecoveryEnabled = true,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(10)
            };

            Console.WriteLine($"[RabbitMQTestHelper] Connecting to RabbitMQ at {factory.HostName}:{factory.Port} as {factory.UserName}");
            Console.WriteLine($"[RabbitMQTestHelper] VirtualHost: '{factory.VirtualHost}' (default)");
            
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            
            // Enable Publisher Confirms - CRITICAL for ensuring messages are actually sent
            _channel.ConfirmSelect();
            Console.WriteLine($"[RabbitMQTestHelper] Publisher Confirms enabled");
            
            Console.WriteLine($"[RabbitMQTestHelper] Successfully connected to RabbitMQ. Connection is open: {_connection.IsOpen}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RabbitMQTestHelper] ERROR connecting to RabbitMQ: {ex.Message}");
            Console.WriteLine($"[RabbitMQTestHelper] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Declares an exchange for testing (idempotent - won't fail if already exists)
    /// </summary>
    public void DeclareExchange(string exchangeName, string exchangeType = ExchangeType.Direct)
    {
        try
        {
            _channel.ExchangeDeclare(exchange: exchangeName, type: exchangeType, durable: true, autoDelete: false);
            _exchanges.Add(exchangeName);
        }
        catch
        {
            // Exchange might already exist - that's fine
        }
    }

    /// <summary>
    /// Creates a queue and binds it to an exchange
    /// For listening to CompensationService output, use CreateListenerQueue
    /// </summary>
    public string CreateQueueAndBind(string exchangeName, string routingKey, bool durable = true)
    {
        // Use durable queue with unique name to avoid conflicts
        var queueName = $"test_{Guid.NewGuid():N}";
        _channel.QueueDeclare(
            queue: queueName,
            durable: durable,
            exclusive: false,
            autoDelete: !durable,
            arguments: null);
        _channel.QueueBind(queue: queueName, exchange: exchangeName, routingKey: routingKey);
        _queues.Add(queueName);
        return queueName;
    }

    /// <summary>
    /// Creates a listener queue that binds to the same routing key as CompensationService
    /// This allows tests to listen for events published by CompensationService
    /// </summary>
    public string CreateListenerQueue(string exchangeName, string routingKey)
    {
        // Ensure exchange exists first
        DeclareExchange(exchangeName, ExchangeType.Direct);
        
        // Create durable queue with unique name for listening to CompensationService output
        var queueName = $"test_listener_{Guid.NewGuid():N}";
        Console.WriteLine($"[RabbitMQTestHelper] Creating listener queue: {queueName}");
        Console.WriteLine($"[RabbitMQTestHelper] Binding to exchange: {exchangeName}, routing key: {routingKey}");
        
        _channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        _channel.QueueBind(queue: queueName, exchange: exchangeName, routingKey: routingKey);
        _queues.Add(queueName);
        
        // Verify queue exists and is bound
        Console.WriteLine($"[RabbitMQTestHelper] Listener queue created and bound: {queueName}");
        Console.WriteLine($"[RabbitMQTestHelper] Queue should be bound to exchange '{exchangeName}' with routing key '{routingKey}'");
        
        // Verify binding exists by checking queue info
        try
        {
            var queueInfo = _channel.QueueDeclarePassive(queueName);
            Console.WriteLine($"[RabbitMQTestHelper] Queue verified: {queueName}, MessageCount: {queueInfo.MessageCount}, ConsumerCount: {queueInfo.ConsumerCount}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RabbitMQTestHelper] WARNING: Could not verify queue: {ex.Message}");
        }
        
        return queueName;
    }

    /// <summary>
    /// Waits for a queue binding to be active by polling RabbitMQ Management API
    /// </summary>
    public async Task WaitForQueueBinding(string queueName, string exchangeName, string routingKey, int maxWaitSeconds = 5)
    {
        Console.WriteLine($"[RabbitMQTestHelper] Waiting for queue binding: queue='{queueName}', exchange='{exchangeName}', routingKey='{routingKey}'");
        var startTime = DateTime.UtcNow;
        var maxWait = TimeSpan.FromSeconds(maxWaitSeconds);
        
        while (DateTime.UtcNow - startTime < maxWait)
        {
            try
            {
                using var httpClient = new HttpClient();
                var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes("guest:guest"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                
                var bindingsResponse = await httpClient.GetAsync("http://localhost:15672/api/exchanges/%2F/book_events/bindings/source");
                if (bindingsResponse.IsSuccessStatusCode)
                {
                    var bindingsJson = await bindingsResponse.Content.ReadAsStringAsync();
                    var bindings = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(bindingsJson);
                    
                    var bindingExists = bindings?.Any(b => 
                        b.ContainsKey("destination") && b["destination"]?.ToString() == queueName &&
                        b.ContainsKey("routing_key") && b["routing_key"]?.ToString() == routingKey) == true;
                    
                    if (bindingExists)
                    {
                        Console.WriteLine($"[RabbitMQTestHelper] Queue binding verified after {(DateTime.UtcNow - startTime).TotalMilliseconds:F0}ms");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RabbitMQTestHelper] Error checking binding: {ex.Message}");
            }
            
            await Task.Delay(200); // Poll every 200ms
        }
        
        Console.WriteLine($"[RabbitMQTestHelper] WARNING: Queue binding not verified after {maxWaitSeconds}s");
    }

    /// <summary>
    /// Consumes messages from an existing queue (e.g., orderservice_queue)
    /// Useful when you want to listen on a queue that already exists
    /// </summary>
    public List<T> ConsumeMessagesFromExistingQueue<T>(string queueName, int expectedCount, TimeSpan timeout)
    {
        Console.WriteLine($"[RabbitMQTestHelper] ConsumeMessagesFromExistingQueue: queue='{queueName}', expectedCount={expectedCount}, timeout={timeout.TotalSeconds}s");
        return ConsumeMessages<T>(queueName, expectedCount, timeout);
    }

    /// <summary>
    /// Publishes a message to an exchange
    /// </summary>
    public void PublishMessage<T>(T message, string exchangeName, string routingKey)
    {
        try
        {
            // Ensure exchange exists
            DeclareExchange(exchangeName, ExchangeType.Direct);
            
            var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var body = Encoding.UTF8.GetBytes(json);

            // Log message before publishing to verify format
            var messagePreview = Encoding.UTF8.GetString(body);
            Console.WriteLine($"[RabbitMQTestHelper] ===== PUBLISHING MESSAGE =====");
            Console.WriteLine($"[RabbitMQTestHelper] Exchange: '{exchangeName}'");
            Console.WriteLine($"[RabbitMQTestHelper] RoutingKey: '{routingKey}'");
            Console.WriteLine($"[RabbitMQTestHelper] Message length: {body.Length} bytes");
            Console.WriteLine($"[RabbitMQTestHelper] First char: '{messagePreview[0]}' (ASCII: {(int)messagePreview[0]})");
            Console.WriteLine($"[RabbitMQTestHelper] Full message: {messagePreview}");

            // Verify connection and channel are open
            if (!_connection.IsOpen)
            {
                throw new InvalidOperationException($"RabbitMQ connection is not open when trying to publish to '{exchangeName}'");
            }
            if (!_channel.IsOpen)
            {
                throw new InvalidOperationException($"RabbitMQ channel is not open when trying to publish to '{exchangeName}'");
            }
            
            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            Console.WriteLine($"[RabbitMQTestHelper] Publishing message - Connection open: {_connection.IsOpen}, Channel open: {_channel.IsOpen}");
            
            // Set up return handler to catch unrouted messages
            var returnReceived = false;
            var returnReason = string.Empty;
            var returnExchange = string.Empty;
            var returnRoutingKey = string.Empty;
            _channel.BasicReturn += (sender, args) =>
            {
                returnReceived = true;
                returnReason = args.ReplyText;
                returnExchange = args.Exchange;
                returnRoutingKey = args.RoutingKey;
                Console.WriteLine($"[RabbitMQTestHelper] ERROR: Message was returned! ReplyCode: {args.ReplyCode}, ReplyText: {args.ReplyText}, Exchange: {args.Exchange}, RoutingKey: {args.RoutingKey}");
            };
            
            // For testing: Use HTTP API as fallback if RabbitMQ.Client has issues
            // This ensures messages actually reach RabbitMQ (curl works, so HTTP API works)
            if (routingKey == "InventoryReservationFailed" && exchangeName == "book_events")
            {
                Console.WriteLine($"[RabbitMQTestHelper] Using HTTP API to publish message (more reliable than RabbitMQ.Client in tests)");
                
                try
                {
                    var messageJson = JsonSerializer.Serialize(message, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    
                    var httpBody = new
                    {
                        properties = new { content_type = "application/json" },
                        routing_key = routingKey,
                        payload = messageJson,
                        payload_encoding = "string"
                    };
                    
                    var httpJson = JsonSerializer.Serialize(httpBody);
                    var httpContent = new StringContent(httpJson, Encoding.UTF8, "application/json");
                    
                    using var httpClient = new HttpClient();
                    var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes("guest:guest"));
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                    
                    var response = httpClient.PostAsync(
                        "http://localhost:15672/api/exchanges/%2F/book_events/publish",
                        httpContent).Result;
                    
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[RabbitMQTestHelper] ===== MESSAGE PUBLISHED SUCCESSFULLY (HTTP API) =====");
                        Console.WriteLine($"[RabbitMQTestHelper] Published to exchange '{exchangeName}' with routing key '{routingKey}' via HTTP API");
                        return; // Success - exit early
                    }
                    else
                    {
                        var errorContent = response.Content.ReadAsStringAsync().Result;
                        throw new InvalidOperationException($"HTTP API publish failed: {response.StatusCode} - {errorContent}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RabbitMQTestHelper] HTTP API publish failed: {ex.Message}, falling back to RabbitMQ.Client");
                    // Fall through to normal RabbitMQ.Client publishing
                }
            }
            
            Console.WriteLine($"[RabbitMQTestHelper] Calling BasicPublish with exchange='{exchangeName}', routingKey='{routingKey}', mandatory=true");
            
            try
            {
                _channel.BasicPublish(
                    exchange: exchangeName,
                    routingKey: routingKey,
                    mandatory: true, // Ensure message is routed
                    basicProperties: properties,
                    body: body);
                
                Console.WriteLine($"[RabbitMQTestHelper] BasicPublish completed without exception");
                
                // Wait for Publisher Confirm - CRITICAL to ensure message was actually sent
                bool confirmed = _channel.WaitForConfirms(TimeSpan.FromSeconds(5));
                Console.WriteLine($"[RabbitMQTestHelper] Publisher Confirm received: {confirmed}");
                
                if (!confirmed)
                {
                    throw new InvalidOperationException("Message was NOT confirmed by RabbitMQ - message may be lost in transit!");
                }
            }
            catch (Exception publishEx)
            {
                Console.WriteLine($"[RabbitMQTestHelper] EXCEPTION during BasicPublish or Confirm: {publishEx.Message}");
                Console.WriteLine($"[RabbitMQTestHelper] Stack trace: {publishEx.StackTrace}");
                throw;
            }
            
            // Wait a moment to see if return is triggered
            System.Threading.Thread.Sleep(200);
            
            if (returnReceived)
            {
                var errorMsg = $"Message was returned by RabbitMQ! Exchange: '{returnExchange}', RoutingKey: '{returnRoutingKey}', ReplyText: '{returnReason}'";
                Console.WriteLine($"[RabbitMQTestHelper] {errorMsg}");
                throw new InvalidOperationException(errorMsg);
            }
            
            Console.WriteLine($"[RabbitMQTestHelper] ===== MESSAGE PUBLISHED SUCCESSFULLY =====");
            Console.WriteLine($"[RabbitMQTestHelper] Published to exchange '{exchangeName}' with routing key '{routingKey}' with confirmation");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[RabbitMQTestHelper] ERROR publishing message: {ex.Message}");
            Console.WriteLine($"[RabbitMQTestHelper] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Consumes messages from a queue
    /// </summary>
    public List<T> ConsumeMessages<T>(string queueName, int expectedCount, TimeSpan timeout)
    {
        Console.WriteLine($"[RabbitMQTestHelper] ConsumeMessages: queue='{queueName}', expectedCount={expectedCount}, timeout={timeout.TotalSeconds}s");
        var messages = new List<T>();
        var startTime = DateTime.UtcNow;
        var checkCount = 0;

        while (messages.Count < expectedCount && DateTime.UtcNow - startTime < timeout)
        {
            checkCount++;
            try
            {
                var result = _channel.BasicGet(queue: queueName, autoAck: true);
                if (result != null)
                {
                    var body = Encoding.UTF8.GetString(result.Body.ToArray());
                    Console.WriteLine($"[RabbitMQTestHelper] ConsumeMessages: Received message #{messages.Count + 1} from queue '{queueName}': {body.Substring(0, Math.Min(100, body.Length))}...");
                    try
                    {
                        var message = JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });
                        if (message != null)
                        {
                            messages.Add(message);
                            Console.WriteLine($"[RabbitMQTestHelper] ConsumeMessages: Successfully deserialized message #{messages.Count}");
                        }
                    }
                    catch (Exception deserEx)
                    {
                        Console.WriteLine($"[RabbitMQTestHelper] ConsumeMessages: Deserialization error: {deserEx.Message}");
                        // Ignore deserialization errors
                    }
                }
                else
                {
                    if (checkCount % 10 == 0) // Log every 10th check
                    {
                        var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
                        Console.WriteLine($"[RabbitMQTestHelper] ConsumeMessages: No message yet (check #{checkCount}, {elapsed:F1}s elapsed)");
                    }
                    Thread.Sleep(100); // Wait a bit before checking again
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RabbitMQTestHelper] ConsumeMessages: ERROR getting message from queue '{queueName}': {ex.Message}");
                Thread.Sleep(100);
            }
        }

        Console.WriteLine($"[RabbitMQTestHelper] ConsumeMessages: Finished. Found {messages.Count} messages (expected {expectedCount})");
        return messages;
    }

    /// <summary>
    /// Purges a specific queue
    /// </summary>
    public void PurgeQueue(string queueName)
    {
        try
        {
            _channel.QueuePurge(queueName);
        }
        catch
        {
            // Ignore if queue doesn't exist
        }
    }

    /// <summary>
    /// Purges all queues created by this helper
    /// </summary>
    public void PurgeQueues()
    {
        foreach (var queue in _queues)
        {
            try
            {
                _channel.QueuePurge(queue);
            }
            catch
            {
                // Ignore if queue doesn't exist
            }
        }
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
    }
}

