using AutoMapper;
using WarehouseService.DTOs;
using WarehouseService.Models;
using WarehouseService.Repositories;
using WarehouseService.Services;
using Microsoft.AspNetCore.Mvc;

namespace WarehouseService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WarehouseController : ControllerBase
{
    private readonly IWarehouseItemRepository _warehouseRepository;
    private readonly IMessageProducer _messageProducer;
    private readonly StockAggregationService _stockAggregationService;
    private readonly IMapper _mapper;
    private readonly ILogger<WarehouseController> _logger;

    public WarehouseController(
        IWarehouseItemRepository warehouseRepository,
        IMessageProducer messageProducer,
        StockAggregationService stockAggregationService,
        IMapper mapper,
        ILogger<WarehouseController> logger)
    {
        _warehouseRepository = warehouseRepository;
        _messageProducer = messageProducer;
        _stockAggregationService = stockAggregationService;
        _mapper = mapper;
        _logger = logger;
    }

    [HttpGet("items")]
    public async Task<ActionResult<IEnumerable<WarehouseItemDto>>> GetAllWarehouseItems()
    {
        try
        {
            var items = await _warehouseRepository.GetAllWarehouseItemsAsync();
            var itemDtos = _mapper.Map<IEnumerable<WarehouseItemDto>>(items);
            return Ok(itemDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all warehouse items");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("items/id/{id}")]
    public async Task<ActionResult<IEnumerable<WarehouseItemDto>>> GetWarehouseItemsByBookIsbn(string bookIsbn)
    {
        try
        {
            var items = await _warehouseRepository.GetWarehouseItemsByBookIsbnAsync(bookIsbn);
            var itemDtos = _mapper.Map<IEnumerable<WarehouseItemDto>>(items);
            return Ok(itemDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouse items for book ISBN {BookIsbn}", bookIsbn);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("sellers/{sellerId}/items")]
    public async Task<ActionResult<IEnumerable<WarehouseItemDto>>> GetWarehouseItemsBySeller(string sellerId)
    {
        try
        {
            var items = await _warehouseRepository.GetWarehouseItemsBySellerAsync(sellerId);
            var itemDtos = _mapper.Map<IEnumerable<WarehouseItemDto>>(items);
            return Ok(itemDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouse items for seller {SellerId}", sellerId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("items/new")]
    public async Task<ActionResult<IEnumerable<WarehouseItemDto>>> GetNewBooks()
    {
        try
        {
            var items = await _warehouseRepository.GetNewBooksAsync();
            var itemDtos = _mapper.Map<IEnumerable<WarehouseItemDto>>(items);
            return Ok(itemDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving new books");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("items/used")]
    public async Task<ActionResult<IEnumerable<WarehouseItemDto>>> GetUsedBooks()
    {
        try
        {
            var items = await _warehouseRepository.GetUsedBooksAsync();
            var itemDtos = _mapper.Map<IEnumerable<WarehouseItemDto>>(items);
            return Ok(itemDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving used books");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("items")]
    public async Task<ActionResult<WarehouseItemDto>> CreateWarehouseItem([FromBody] CreateWarehouseItemDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if item already exists for this book and seller
            var existingItem = await _warehouseRepository.GetWarehouseItemByBookAndSellerAsync(createDto.BookISBN, createDto.SellerId);
            if (existingItem != null)
            {
                return Conflict($"Warehouse item already exists for book ISBN {createDto.BookISBN} and seller {createDto.SellerId}");
            }

            var item = _mapper.Map<WarehouseItem>(createDto);
            var createdItem = await _warehouseRepository.AddWarehouseItemAsync(item);

            // Publish event with aggregated stock data
            await _stockAggregationService.PublishAggregatedStockEventAsync(createdItem.BookISBN);

            var itemDto = _mapper.Map<WarehouseItemDto>(createdItem);
            return CreatedAtAction(nameof(GetWarehouseItemById), new { id = itemDto.Id }, itemDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating warehouse item");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("items/{id}")]
    public async Task<ActionResult<WarehouseItemDto>> UpdateWarehouseItem(int id, [FromBody] UpdateWarehouseItemDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingItem = await _warehouseRepository.GetWarehouseItemByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound($"Warehouse item with ID {id} not found");
            }

            // Apply updates - OPDATERET: Condition er fjernet, IsNew tilføjet
            if (updateDto.Quantity.HasValue)
                existingItem.Quantity = updateDto.Quantity.Value;
            if (updateDto.Price.HasValue)
                existingItem.Price = updateDto.Price.Value;
            if (updateDto.IsNew.HasValue)
                existingItem.IsNew = updateDto.IsNew.Value;
            if (!string.IsNullOrEmpty(updateDto.Location))
                existingItem.Location = updateDto.Location;

            var updatedItem = await _warehouseRepository.UpdateWarehouseItemAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound($"Warehouse item with ID {id} not found");
            }

            // Publish event with aggregated stock data
            await _stockAggregationService.PublishAggregatedStockEventAsync(updatedItem.BookISBN);

            var itemDto = _mapper.Map<WarehouseItemDto>(updatedItem);
            return Ok(itemDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating warehouse item with ID {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("adjust-stock")]
    public async Task<IActionResult> AdjustStock([FromBody] AdjustStockDto adjustDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var item = await _warehouseRepository.GetWarehouseItemByBookAndSellerAsync(adjustDto.BookISBN, adjustDto.SellerId);
            if (item == null)
            {
                return NotFound($"Warehouse item not found for book ISBN {adjustDto.BookISBN} and seller {adjustDto.SellerId}");
            }

            // Adjust quantity
            item.Quantity += adjustDto.QuantityChange;

            // Ensure quantity doesn't go below 0
            if (item.Quantity < 0)
            {
                item.Quantity = 0;
            }

            var updatedItem = await _warehouseRepository.UpdateWarehouseItemAsync(item.Id, item);
            if (updatedItem == null)
            {
                return NotFound($"Warehouse item with ID {item.Id} not found");
            }

            // Publish event with aggregated stock data
            await _stockAggregationService.PublishAggregatedStockEventAsync(updatedItem.BookISBN);

            return Ok(new { message = "Stock adjusted successfully", newQuantity = updatedItem.Quantity });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adjusting stock for book ISBN {BookIsbn} and seller {SellerId}",
                adjustDto.BookISBN, adjustDto.SellerId);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("items/{id}")]
    public async Task<ActionResult<WarehouseItemDto>> GetWarehouseItemById(int id)
    {
        try
        {
            var item = await _warehouseRepository.GetWarehouseItemByIdAsync(id);
            if (item == null)
            {
                return NotFound($"Warehouse item with ID {id} not found");
            }

            var itemDto = _mapper.Map<WarehouseItemDto>(item);
            return Ok(itemDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving warehouse item with ID {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("items/{id}")]
    public async Task<IActionResult> DeleteWarehouseItem(int id)
    {
        try
        {
            // Get warehouse item before deletion to publish correct event data
            var item = await _warehouseRepository.GetWarehouseItemByIdAsync(id);
            if (item == null)
            {
                return NotFound($"Warehouse item with ID {id} not found");
            }

            var deleted = await _warehouseRepository.DeleteWarehouseItemAsync(id);
            if (!deleted)
            {
                return NotFound($"Warehouse item with ID {id} not found");
            }

            // Publish event for stock removal with correct data
            _messageProducer.SendMessage(new 
            { 
                Id = id, 
                BookISBN = item.BookISBN, 
                SellerId = item.SellerId 
            }, "BookStockRemoved");

            // Also publish aggregated stock update event since stock changed
            await _stockAggregationService.PublishAggregatedStockEventAsync(item.BookISBN);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting warehouse item with ID {Id}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("sync-events")]
    public async Task<ActionResult<int>> SyncEvents()
    {
        try
        {
            var items = await _warehouseRepository.GetAllWarehouseItemsAsync();
            var itemList = items.ToList();
            _logger.LogInformation("Retrieved {ItemCount} warehouse items from database for sync", itemList.Count);
            int totalSyncedCount = 0;
            const int batchSize = 1000;
            const int batchDelayMs = 100;

            for (int i = 0; i < itemList.Count; i += batchSize)
            {
                var batch = itemList.Skip(i).Take(batchSize);
                int batchCount = 0;

                // Group by BookISBN to aggregate
                var groupedByISBN = batch.GroupBy(item => item.BookISBN);
                foreach (var group in groupedByISBN)
                {
                    await _stockAggregationService.PublishAggregatedStockEventAsync(group.Key);
                    batchCount += group.Count();
                    totalSyncedCount += group.Count();
                }

                _logger.LogInformation("Synced batch {BatchNumber}: {BatchCount} items (Total: {TotalCount})",
                    (i / batchSize) + 1, batchCount, totalSyncedCount);

                if (i + batchSize < itemList.Count)
                {
                    await Task.Delay(batchDelayMs);
                }
            }

            _logger.LogInformation("Completed syncing {TotalCount} warehouse items via RabbitMQ events", totalSyncedCount);
            return Ok(totalSyncedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing warehouse events");
            return StatusCode(500, "Internal server error");
        }
    }

}