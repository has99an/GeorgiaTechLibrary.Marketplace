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
    private readonly IMapper _mapper;
    private readonly ILogger<WarehouseController> _logger;

    public WarehouseController(
        IWarehouseItemRepository warehouseRepository,
        IMessageProducer messageProducer,
        IMapper mapper,
        ILogger<WarehouseController> logger)
    {
        _warehouseRepository = warehouseRepository;
        _messageProducer = messageProducer;
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

    [HttpGet("items/{bookIsbn}")]
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

            // Publish event
            _messageProducer.SendMessage(createdItem, "BookStockUpdated");

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

            // Apply updates
            if (updateDto.Quantity.HasValue)
                existingItem.Quantity = updateDto.Quantity.Value;
            if (updateDto.Price.HasValue)
                existingItem.Price = updateDto.Price.Value;
            if (!string.IsNullOrEmpty(updateDto.Condition))
                existingItem.Condition = updateDto.Condition;

            var updatedItem = await _warehouseRepository.UpdateWarehouseItemAsync(id, existingItem);
            if (updatedItem == null)
            {
                return NotFound($"Warehouse item with ID {id} not found");
            }

            // Publish event
            _messageProducer.SendMessage(updatedItem, "BookStockUpdated");

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

            // Publish event
            _messageProducer.SendMessage(updatedItem, "BookStockUpdated");

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
}
