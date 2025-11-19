# ADR-003: MediatR for In-Process Messaging

## Status
Accepted

## Context
In Clean Architecture with CQRS, we need a way to:
1. Decouple controllers from handlers
2. Implement cross-cutting concerns (logging, validation, caching)
3. Keep controllers thin
4. Enable testability

## Decision
We have adopted MediatR as our in-process messaging library.

### What is MediatR?
MediatR is a simple mediator implementation that:
- Dispatches requests to appropriate handlers
- Supports pipeline behaviors for cross-cutting concerns
- Enables loose coupling between components
- Works seamlessly with dependency injection

### Usage Pattern
```csharp
// 1. Define Request
public record SearchBooksQuery(string SearchTerm) : IRequest<SearchBooksResult>;

// 2. Define Handler
public class SearchBooksQueryHandler : IRequestHandler<SearchBooksQuery, SearchBooksResult>
{
    public async Task<SearchBooksResult> Handle(SearchBooksQuery request, CancellationToken ct)
    {
        // Implementation
    }
}

// 3. Use in Controller
[HttpGet]
public async Task<ActionResult> SearchBooks([FromQuery] string query)
{
    var result = await _mediator.Send(new SearchBooksQuery(query));
    return Ok(result);
}
```

### Pipeline Behaviors
MediatR's pipeline behaviors allow us to implement cross-cutting concerns:

```csharp
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
```

**Execution Order:**
```
Request → ValidationBehavior → LoggingBehavior → PerformanceBehavior → CachingBehavior → Handler
```

## Consequences

### Positive
1. **Loose Coupling**: Controllers don't know about handlers
2. **Single Responsibility**: Each handler does one thing
3. **Cross-Cutting Concerns**: Behaviors handle logging, validation, etc.
4. **Testability**: Easy to test handlers in isolation
5. **Consistency**: All requests follow the same pattern
6. **Discoverability**: Easy to find all handlers
7. **Type Safety**: Compile-time checking of request/response types

### Negative
1. **Indirection**: Extra layer between controller and business logic
2. **Learning Curve**: Team needs to learn MediatR
3. **Debugging**: Can be harder to follow execution flow
4. **Performance**: Minimal overhead from reflection (negligible in practice)

### Neutral
1. **More Files**: One file per query/command + handler
2. **Convention**: Need to follow naming conventions

## Implementation Details

### Registration
```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(SearchBooksQuery).Assembly);
});
```

### Validation Integration
```csharp
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var context = new ValidationContext<TRequest>(request);
        var failures = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, ct)));
        
        if (failures.Any(f => !f.IsValid))
        {
            throw new ValidationException(/* ... */);
        }

        return await next();
    }
}
```

### Caching Integration
```csharp
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        // Only cache queries
        if (!typeof(TRequest).Name.EndsWith("Query"))
            return await next();

        var cacheKey = GenerateCacheKey(request);
        var cached = await _cache.GetAsync<TResponse>(cacheKey);
        
        if (cached != null)
            return cached;

        var response = await next();
        await _cache.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5));
        
        return response;
    }
}
```

## Alternatives Considered

### 1. Direct Handler Injection
```csharp
public SearchController(SearchBooksQueryHandler handler)
```
**Rejected**: Tight coupling, no pipeline behaviors

### 2. Custom Mediator Implementation
**Rejected**: Reinventing the wheel, MediatR is battle-tested

### 3. Event-Driven Architecture with Message Bus
**Rejected**: Too complex for in-process communication

## Best Practices

1. **Naming Convention**: 
   - Queries: `Get*Query`, `Search*Query`
   - Commands: `Create*Command`, `Update*Command`, `Delete*Command`

2. **Immutability**: Use `record` types for requests

3. **Single Purpose**: One handler per request type

4. **Validation**: Always validate commands, optionally validate queries

5. **Error Handling**: Let exceptions bubble up to middleware

## References
- [MediatR GitHub](https://github.com/jbogard/MediatR)
- [CQRS with MediatR](https://code-maze.com/cqrs-mediatr-in-aspnet-core/)
- [Jimmy Bogard on MediatR](https://jimmybogard.com/tag/mediatr/)

