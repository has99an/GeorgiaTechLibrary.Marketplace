using OrderService.Application.DTOs;

namespace OrderService.Application.Interfaces;

public interface ICheckoutService
{
    Task<CheckoutSessionDto> CreateCheckoutSessionAsync(string customerId, AddressDto deliveryAddress);
    Task<OrderDto> ConfirmPaymentAsync(string sessionId, string paymentMethod);
    Task<CheckoutSessionDto?> GetCheckoutSessionAsync(string sessionId);
}
