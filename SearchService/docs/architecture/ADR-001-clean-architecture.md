# ADR-001: Adoption of Clean Architecture

## Status
Accepted

## Context
The SearchService was initially built with a simple 3-layer architecture (Controllers → Repositories → Redis). As the service grew, several issues emerged:

1. **Tight Coupling**: Controllers directly depended on repositories, making testing difficult
2. **Mixed Concerns**: Business logic was scattered between controllers and repositories
3. **No Separation of Concerns**: Data access, business logic, and presentation were intertwined
4. **Difficult to Test**: No clear boundaries made unit testing challenging
5. **Hard to Maintain**: Changes in one layer often required changes in multiple places

## Decision
We have decided to refactor SearchService to follow Clean Architecture principles with the following layers:

### 1. Domain Layer (Core)
- **Purpose**: Contains business logic and domain entities
- **Dependencies**: None (completely independent)
- **Contents**:
  - Entities (Book)
  - Value Objects (ISBN, StockInfo, PriceInfo)
  - Domain Services (ISearchIndexService)
  - Specifications (AvailableBooksSpecification)
  - Domain Exceptions

### 2. Application Layer (Use Cases)
- **Purpose**: Contains application-specific business rules
- **Dependencies**: Domain Layer only
- **Contents**:
  - Queries (CQRS Read Side)
  - Commands (CQRS Write Side)
  - Handlers (MediatR)
  - Validators (FluentValidation)
  - Pipeline Behaviors
  - Interfaces (IBookRepository, ICacheService)
  - DTOs

### 3. Infrastructure Layer (Implementation)
- **Purpose**: Implements interfaces defined in Application Layer
- **Dependencies**: Application Layer, Domain Layer
- **Contents**:
  - Redis Repositories
  - Search Index Implementation
  - Caching Service
  - RabbitMQ Consumer
  - External Service Clients

### 4. API Layer (Presentation)
- **Purpose**: Exposes HTTP endpoints
- **Dependencies**: Application Layer
- **Contents**:
  - Controllers (thin, only MediatR dispatching)
  - Middleware
  - Extension Methods
  - API Configuration

## Consequences

### Positive
1. **Testability**: Each layer can be tested independently
2. **Maintainability**: Clear separation of concerns makes code easier to understand
3. **Flexibility**: Easy to swap implementations (e.g., Redis → Elasticsearch)
4. **Scalability**: Well-defined boundaries support horizontal scaling
5. **Domain-Centric**: Business logic is isolated and protected
6. **Dependency Rule**: Dependencies point inward, protecting the domain

### Negative
1. **Complexity**: More files and folders to navigate
2. **Learning Curve**: Team needs to understand Clean Architecture principles
3. **Initial Development Time**: Takes longer to set up initially
4. **Potential Over-Engineering**: May be overkill for very simple features

### Neutral
1. **More Abstractions**: Interfaces for everything increases abstraction level
2. **File Count**: Significantly more files than before

## Implementation Notes
- Used MediatR for CQRS implementation
- FluentValidation for input validation
- AutoMapper for DTO mapping
- Dependency Injection for all layer dependencies

## References
- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [.NET Clean Architecture Template](https://github.com/jasontaylordev/CleanArchitecture)

