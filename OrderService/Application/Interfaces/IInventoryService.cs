namespace OrderService.Application.Interfaces;

/// <summary>
/// Service interface for inventory operations
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Checks if a book is available in the specified quantity
    /// </summary>
    Task<bool> CheckAvailabilityAsync(string bookISBN, string sellerId, int quantity);

    /// <summary>
    /// Reserves inventory for an order
    /// </summary>
    Task<bool> ReserveInventoryAsync(Guid orderId, string bookISBN, string sellerId, int quantity);

    /// <summary>
    /// Releases reserved inventory (on cancellation)
    /// </summary>
    Task ReleaseInventoryAsync(Guid orderId, string bookISBN, string sellerId, int quantity);

    /// <summary>
    /// Restores inventory (on refund)
    /// </summary>
    Task RestoreInventoryAsync(Guid orderId, string bookISBN, string sellerId, int quantity);
}

