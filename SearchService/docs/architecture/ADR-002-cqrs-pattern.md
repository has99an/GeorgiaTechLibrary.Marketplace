# ADR-002: CQRS Pattern with MediatR

## Status
Accepted

## Context
The SearchService handles both read operations (searches, queries) and write operations (indexing, updates). In the original implementation:

1. **Mixed Operations**: Read and write operations were handled by the same repository methods
2. **Optimization Challenges**: Difficult to optimize reads differently from writes
3. **Scalability**: Cannot scale reads and writes independently
4. **Complexity**: Single models trying to serve both read and write needs

## Decision
We have adopted the Command Query Responsibility Segregation (CQRS) pattern using MediatR:

### Commands (Write Operations)
- `CreateBookCommand`: Add new book to index
- `UpdateBookCommand`: Update existing book
- `DeleteBookCommand`: Remove book from index
- `UpdateBookStockCommand`: Update stock information

**Characteristics:**
- Modify state
- Return success/failure results
- May trigger side effects
- Validated with FluentValidation

### Queries (Read Operations)
- `SearchBooksQuery`: Search for books by term
- `GetAvailableBooksQuery`: Get paginated available books
- `GetBookByIsbnQuery`: Get specific book
- `GetFeaturedBooksQuery`: Get random featured books
- `GetBookSellersQuery`: Get sellers for a book
- `GetSearchStatsQuery`: Get search statistics

**Characteristics:**
- Never modify state
- Return data
- Can be cached
- Optimized for read performance

### MediatR Implementation
```csharp
// Query
public record SearchBooksQuery(string SearchTerm) : IRequest<SearchBooksResult>;

// Handler
public class SearchBooksQueryHandler : IRequestHandler<SearchBooksQuery, SearchBooksResult>
{
    public async Task<SearchBooksResult> Handle(SearchBooksQuery request, CancellationToken ct)
    {
        // Implementation
    }
}

// Usage in Controller
var result = await _mediator.Send(new SearchBooksQuery(query));
```

## Consequences

### Positive
1. **Separation of Concerns**: Read and write logic completely separated
2. **Optimization**: Can optimize queries independently (caching, indexing)
3. **Scalability**: Can scale read and write sides independently
4. **Testability**: Easy to test queries and commands in isolation
5. **Pipeline Behaviors**: Cross-cutting concerns (logging, validation, caching) applied automatically
6. **Thin Controllers**: Controllers become simple MediatR dispatchers

### Negative
1. **More Code**: Separate classes for each query/command
2. **Learning Curve**: Team needs to understand CQRS and MediatR
3. **Potential Duplication**: Some logic may be duplicated between read and write sides

### Pipeline Behaviors
We implemented the following behaviors:
1. **ValidationBehavior**: Automatic validation using FluentValidation
2. **LoggingBehavior**: Request/response logging
3. **PerformanceBehavior**: Slow request detection
4. **CachingBehavior**: Automatic query result caching

## Implementation Notes
- All queries are immutable records
- All commands are immutable records
- Handlers are scoped services
- Behaviors are transient services
- Validation happens before handler execution

## Alternatives Considered
1. **Traditional Repository Pattern**: Rejected due to mixed concerns
2. **Event Sourcing**: Too complex for current needs
3. **Direct Database Access**: Would bypass domain logic

## References
- [CQRS Pattern](https://martinfowler.com/bliki/CQRS.html)
- [MediatR Documentation](https://github.com/jbogard/MediatR)
- [CQRS Journey](https://docs.microsoft.com/en-us/previous-versions/msp-n-p/jj554200(v=pandp.10))

