using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

var factory = new ConnectionFactory
{
    HostName = "localhost",
    Port = 5672,
    UserName = "guest",
    Password = "guest"
};

using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.ConfirmSelect();

var testEvent = new { OrderId = "direct-test-123", orderItemId = "direct-item-456", bookISBN = "9780123456789", sellerId = "direct-seller-789", quantity = 1, errorMessage = "Direct test", failedAt = "2026-01-08T00:00:00Z", retryAttempts = 5 };
var json = JsonSerializer.Serialize(testEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
var body = Encoding.UTF8.GetBytes(json);

var properties = channel.CreateBasicProperties();
properties.Persistent = true;
properties.ContentType = "application/json";

Console.WriteLine($"Publishing to exchange 'book_events' with routing key 'InventoryReservationFailed'");
channel.BasicPublish(
    exchange: "book_events",
    routingKey: "InventoryReservationFailed",
    mandatory: true,
    basicProperties: properties,
    body: body);

bool confirmed = channel.WaitForConfirms(TimeSpan.FromSeconds(5));
Console.WriteLine($"Confirmed: {confirmed}");
