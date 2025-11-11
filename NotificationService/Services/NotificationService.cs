using Microsoft.Extensions.Logging;
using NotificationService.Models;

namespace NotificationService.Services;

public class NotificationService
{
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ILogger<NotificationService> logger)
    {
        _logger = logger;
    }

    public void SendNotificationToSeller(string sellerId, Guid orderId, string bookISBN, int quantity)
    {
        // Simulate sending email/SMS notification
        _logger.LogInformation("📧 Sending notification to seller {SellerId}: Order {OrderId} - Please ship {Quantity}x of book {BookISBN}",
            sellerId, orderId, quantity, bookISBN);

        // In a real implementation, this would:
        // - Send email via SMTP
        // - Send SMS via SMS gateway
        // - Use notification service like SendGrid, Twilio, etc.
    }
}
