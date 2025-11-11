using AutoMapper;
using BookService.DTOs;
using BookService.Models;
using BookService.Repositories;
using BookService.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BooksController : ControllerBase
{
    private readonly IBookRepository _bookRepository;
    private readonly IMessageProducer _messageProducer;
    private readonly IMapper _mapper;
    private readonly ILogger<BooksController> _logger;

    public BooksController(
        IBookRepository bookRepository,
        IMessageProducer messageProducer,
        IMapper mapper,
        ILogger<BooksController> logger)
    {
        _bookRepository = bookRepository;
        _messageProducer = messageProducer;
        _mapper = mapper;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookDto>>> GetAllBooks()
    {
        try
        {
            var books = await _bookRepository.GetAllBooksAsync();
            var bookDtos = _mapper.Map<IEnumerable<BookDto>>(books);
            return Ok(bookDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all books");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{isbn}")]
    public async Task<ActionResult<BookDto>> GetBookByIsbn(string isbn)
    {
        try
        {
            var book = await _bookRepository.GetBookByIsbnAsync(isbn);
            if (book == null)
            {
                return NotFound($"Book with ISBN {isbn} not found");
            }

            var bookDto = _mapper.Map<BookDto>(book);
            return Ok(bookDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving book with ISBN {Isbn}", isbn);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<BookDto>> CreateBook([FromBody] CreateBookDto createBookDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingBook = await _bookRepository.BookExistsAsync(createBookDto.ISBN);
            if (existingBook)
            {
                return Conflict($"Book with ISBN {createBookDto.ISBN} already exists");
            }

            var book = _mapper.Map<Book>(createBookDto);
            var createdBook = await _bookRepository.AddBookAsync(book);

            // Publish event
            var bookCreatedEvent = _mapper.Map<BookEvent>(createdBook);
            _messageProducer.SendMessage(bookCreatedEvent, "BookCreated");

            var bookDto = _mapper.Map<BookDto>(createdBook);
            return CreatedAtAction(nameof(GetBookByIsbn), new { isbn = bookDto.ISBN }, bookDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating book");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{isbn}")]
    public async Task<ActionResult<BookDto>> UpdateBook(string isbn, [FromBody] UpdateBookDto updateBookDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var existingBook = await _bookRepository.GetBookByIsbnAsync(isbn);
            if (existingBook == null)
            {
                return NotFound($"Book with ISBN {isbn} not found");
            }

            // Apply updates
            if (!string.IsNullOrEmpty(updateBookDto.BookTitle))
                existingBook.BookTitle = updateBookDto.BookTitle;
            if (!string.IsNullOrEmpty(updateBookDto.BookAuthor))
                existingBook.BookAuthor = updateBookDto.BookAuthor;
            if (updateBookDto.YearOfPublication.HasValue)
                existingBook.YearOfPublication = updateBookDto.YearOfPublication.Value;
            if (!string.IsNullOrEmpty(updateBookDto.Publisher))
                existingBook.Publisher = updateBookDto.Publisher;
            if (updateBookDto.ImageUrlS != null)
                existingBook.ImageUrlS = updateBookDto.ImageUrlS;
            if (updateBookDto.ImageUrlM != null)
                existingBook.ImageUrlM = updateBookDto.ImageUrlM;
            if (updateBookDto.ImageUrlL != null)
                existingBook.ImageUrlL = updateBookDto.ImageUrlL;

            var updatedBook = await _bookRepository.UpdateBookAsync(isbn, existingBook);
            if (updatedBook == null)
            {
                return NotFound($"Book with ISBN {isbn} not found");
            }

            // Publish event
            var bookUpdatedEvent = _mapper.Map<BookEvent>(updatedBook);
            _messageProducer.SendMessage(bookUpdatedEvent, "BookUpdated");

            var bookDto = _mapper.Map<BookDto>(updatedBook);
            return Ok(bookDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating book with ISBN {Isbn}", isbn);
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{isbn}")]
    public async Task<IActionResult> DeleteBook(string isbn)
    {
        try
        {
            var book = await _bookRepository.GetBookByIsbnAsync(isbn);
            if (book == null)
            {
                return NotFound($"Book with ISBN {isbn} not found");
            }

            var deleted = await _bookRepository.DeleteBookAsync(isbn);
            if (!deleted)
            {
                return NotFound($"Book with ISBN {isbn} not found");
            }

            // Publish event
            var bookDeletedEvent = _mapper.Map<BookEvent>(book);
            _messageProducer.SendMessage(bookDeletedEvent, "BookDeleted");

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting book with ISBN {Isbn}", isbn);
            return StatusCode(500, "Internal server error");
        }
    }
}
