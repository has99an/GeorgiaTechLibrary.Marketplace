# Scaling Strategy - Georgia Tech Library Marketplace

## Overview

This document outlines the scaling strategy for the Georgia Tech Library Marketplace microservices architecture. The system is designed to scale both horizontally and vertically to support growth over the next 5 years.

## Architecture Principles

- **Event-Driven Architecture**: Services communicate asynchronously via RabbitMQ
- **Database per Service**: Each service has its own database (no shared database)
- **Microservices**: 8 independent services that can scale independently
- **Containerization**: All services run in Docker containers

## 1. Horizontal Scaling

### 1.1 Service Replication

Each service can be scaled horizontally by running multiple instances behind a load balancer.

#### Docker Swarm Setup

```yaml
# docker-compose.scale.yml
version: '3.8'
services:
  searchservice:
    deploy:
      replicas: 3
      update_config:
        parallelism: 1
        delay: 10s
      restart_policy:
        condition: on-failure
        max_attempts: 3
```

#### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: searchservice
spec:
  replicas: 3
  selector:
    matchLabels:
      app: searchservice
  template:
    metadata:
      labels:
        app: searchservice
    spec:
      containers:
      - name: searchservice
        image: searchservice:latest
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
```

### 1.2 Load Balancer Configuration

#### ApiGateway Load Balancing

The ApiGateway (YARP) can distribute traffic across multiple service instances:

```json
{
  "ReverseProxy": {
    "Clusters": {
      "search-cluster": {
        "Destinations": {
          "search-destination-1": {
            "Address": "http://searchservice-1:8080"
          },
          "search-destination-2": {
            "Address": "http://searchservice-2:8080"
          },
          "search-destination-3": {
            "Address": "http://searchservice-3:8080"
          }
        },
        "LoadBalancingPolicy": "RoundRobin"
      }
    }
  }
}
```

### 1.3 Scaling Recommendations by Service

| Service | Initial Replicas | Target Replicas (Year 1) | Target Replicas (Year 5) | Scaling Priority |
|---------|------------------|--------------------------|-------------------------|------------------|
| SearchService | 2 | 5 | 10 | High (read-heavy) |
| ApiGateway | 2 | 3 | 5 | High (entry point) |
| OrderService | 1 | 2 | 4 | Medium |
| BookService | 1 | 2 | 3 | Low (mostly reads) |
| WarehouseService | 1 | 2 | 3 | Medium |
| UserService | 1 | 2 | 3 | Medium |
| AuthService | 1 | 2 | 3 | Medium |
| NotificationService | 1 | 2 | 3 | Low (async) |

## 2. Vertical Scaling

### 2.1 Resource Requirements

#### Minimum Requirements (Development)

| Service | CPU | Memory | Storage |
|---------|-----|--------|---------|
| ApiGateway | 0.25 cores | 256 MB | 100 MB |
| AuthService | 0.25 cores | 256 MB | 500 MB |
| BookService | 0.25 cores | 256 MB | 500 MB |
| WarehouseService | 0.25 cores | 256 MB | 500 MB |
| SearchService | 0.5 cores | 512 MB | 1 GB |
| OrderService | 0.25 cores | 256 MB | 500 MB |
| UserService | 0.25 cores | 256 MB | 500 MB |
| NotificationService | 0.25 cores | 256 MB | 500 MB |

#### Production Requirements (Year 1)

| Service | CPU | Memory | Storage |
|---------|-----|--------|---------|
| ApiGateway | 1 core | 1 GB | 500 MB |
| AuthService | 0.5 cores | 512 MB | 2 GB |
| BookService | 0.5 cores | 512 MB | 2 GB |
| WarehouseService | 1 core | 1 GB | 5 GB |
| SearchService | 2 cores | 4 GB | 10 GB |
| OrderService | 1 core | 1 GB | 5 GB |
| UserService | 1 core | 1 GB | 5 GB |
| NotificationService | 0.5 cores | 512 MB | 2 GB |

#### Production Requirements (Year 5)

| Service | CPU | Memory | Storage |
|---------|-----|--------|---------|
| ApiGateway | 2 cores | 2 GB | 1 GB |
| AuthService | 1 core | 1 GB | 10 GB |
| BookService | 1 core | 1 GB | 10 GB |
| WarehouseService | 2 cores | 4 GB | 50 GB |
| SearchService | 4 cores | 8 GB | 50 GB |
| OrderService | 2 cores | 4 GB | 50 GB |
| UserService | 2 cores | 4 GB | 50 GB |
| NotificationService | 1 core | 1 GB | 10 GB |

### 2.2 Database Sizing

Each service has its own SQL Server database:

| Database | Initial Size | Year 1 Size | Year 5 Size |
|----------|--------------|-------------|-------------|
| BookDb | 1 GB | 5 GB | 20 GB |
| WarehouseServiceDb | 2 GB | 10 GB | 100 GB |
| OrderServiceDb | 1 GB | 10 GB | 100 GB |
| UserServiceDb | 500 MB | 5 GB | 50 GB |
| AuthServiceDb | 500 MB | 2 GB | 10 GB |
| NotificationServiceDb | 500 MB | 2 GB | 10 GB |

**Note**: SearchService uses Redis (no SQL database)

## 3. Database Scaling

### 3.1 Connection Pooling

Configure connection pooling per service:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=sqlserver;Database=BookDb;User Id=sa;Password=***;TrustServerCertificate=true;Max Pool Size=100;Min Pool Size=10;Connection Timeout=30;"
  }
}
```

### 3.2 Read Replicas

For read-heavy services (SearchService, BookService), implement read replicas:

```csharp
// Example: Read replica configuration
services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.UseReadReplica(
            configuration.GetConnectionString("ReadReplicaConnection"))));
```

### 3.3 Database Sharding (Future)

For very large datasets (Year 5+), consider sharding:

- **OrderServiceDb**: Shard by customer ID or date range
- **WarehouseServiceDb**: Shard by seller ID or book category
- **UserServiceDb**: Shard by user ID range

## 4. Event-Driven Scaling

### 4.1 RabbitMQ Cluster Setup

For high availability and performance:

```yaml
# RabbitMQ cluster configuration
rabbitmq:
  image: rabbitmq:3-management
  environment:
    RABBITMQ_ERLANG_COOKIE: "secret_cookie"
    RABBITMQ_DEFAULT_USER: admin
    RABBITMQ_DEFAULT_PASS: admin
  volumes:
    - rabbitmq_data:/var/lib/rabbitmq
  deploy:
    replicas: 3
```

### 4.2 Queue Partitioning

Partition queues by service or message type:

- `book_events` → Partition by ISBN range
- `order_events` → Partition by order ID hash
- `user_events` → Partition by user ID range

### 4.3 Consumer Scaling

Scale consumers independently:

```csharp
// Configure multiple consumers per service
services.AddHostedService<RabbitMQConsumer>();
// Or use multiple instances:
services.AddHostedService<RabbitMQConsumer>();
services.AddHostedService<RabbitMQConsumer>();
```

### 4.4 Dead Letter Queue Handling

Configure DLQ for failed messages:

```csharp
channel.QueueDeclare(
    queue: "book_events_dlq",
    durable: true,
    exclusive: false,
    autoDelete: false);
```

## 5. Caching Strategy

### 5.1 Redis Cluster

For SearchService, use Redis cluster for high availability:

```yaml
redis:
  image: redis:7-alpine
  command: redis-server --cluster-enabled yes
  deploy:
    replicas: 3
```

### 5.2 Cache Invalidation Patterns

- **Event-Driven Invalidation**: Invalidate cache when data changes via events
- **TTL-Based Expiration**: Set appropriate TTLs (5 minutes for search results)
- **Cache Warming**: Pre-warm cache for popular searches

### 5.3 Cache Warming Strategies

```csharp
// Background service to warm cache
public class CacheWarmerService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Warm popular searches every 5 minutes
        while (!stoppingToken.IsCancellationRequested)
        {
            await WarmPopularSearches();
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

## 6. Organizational Scaling

### 6.1 Team Structure (Current - 10 people)

- **Business Analysts**: 2
- **Project Managers**: 1
- **Designers**: 1
- **Developers**: 6

### 6.2 Team Structure (Year 1 - 20 people)

- **Product Team**: 3 (Product Manager, 2 Business Analysts)
- **Engineering Team**: 12
  - **Backend Developers**: 6 (2 per service area)
  - **Frontend Developers**: 3
  - **DevOps Engineers**: 2
  - **QA Engineers**: 1
- **Design Team**: 2
- **Project Management**: 2
- **Support**: 1

### 6.3 Team Structure (Year 5 - 50+ people)

- **Product Team**: 5
- **Engineering Team**: 30
  - **Backend Developers**: 15 (organized by service teams)
  - **Frontend Developers**: 8
  - **DevOps/SRE**: 5
  - **QA Engineers**: 2
- **Design Team**: 5
- **Project Management**: 5
- **Support**: 5

### 6.4 Development Workflow

#### Current (Small Team)
- Feature branches
- Code review by team lead
- Manual deployment

#### Year 1 (Medium Team)
- Feature branches
- Automated CI/CD
- Code review by 2+ reviewers
- Staging environment
- Automated testing

#### Year 5 (Large Team)
- GitFlow or Trunk-based development
- Automated CI/CD with multiple stages
- Mandatory code review
- Multiple environments (dev, staging, pre-prod, prod)
- Feature flags
- Canary deployments
- Automated rollback

### 6.5 Deployment Processes

#### Current
- Manual Docker Compose deployment
- Single environment

#### Year 1
- Automated deployment via CI/CD
- Blue-green deployment
- Health check validation before traffic switch

#### Year 5
- Kubernetes-based deployment
- Canary deployments
- A/B testing infrastructure
- Automated rollback on health check failures
- Multi-region deployment

### 6.6 Monitoring and Alerting

#### Current
- Basic health checks
- Manual monitoring

#### Year 1
- Application Insights or Prometheus
- Grafana dashboards
- Alerting on error rates and response times
- Log aggregation (ELK stack)

#### Year 5
- Comprehensive observability (metrics, logs, traces)
- AI-powered anomaly detection
- Predictive scaling
- Multi-region monitoring
- SLA/SLO tracking

## 7. Performance Targets

### 7.1 Current Targets (Year 1)

- **Throughput**: 1000 requests/min
- **Search Response Time**: < 1 second (p95)
- **API Response Time**: < 500ms (p95)
- **Availability**: 99.5% uptime

### 7.2 Future Targets (Year 5)

- **Throughput**: 10,000 requests/min
- **Search Response Time**: < 500ms (p95)
- **API Response Time**: < 200ms (p95)
- **Availability**: 99.9% uptime

## 8. Cost Optimization

### 8.1 Infrastructure Costs (Year 1)

- **Compute**: ~$500/month (cloud VMs)
- **Database**: ~$300/month (managed SQL Server)
- **Message Broker**: ~$100/month (RabbitMQ)
- **Cache**: ~$50/month (Redis)
- **Total**: ~$950/month

### 8.2 Infrastructure Costs (Year 5)

- **Compute**: ~$5,000/month (Kubernetes cluster)
- **Database**: ~$3,000/month (managed SQL Server with replicas)
- **Message Broker**: ~$500/month (RabbitMQ cluster)
- **Cache**: ~$500/month (Redis cluster)
- **CDN**: ~$200/month
- **Monitoring**: ~$300/month
- **Total**: ~$9,500/month

## 9. Scaling Checklist

### Immediate (Now)
- [x] Docker containerization
- [x] Health checks implemented
- [x] CI/CD pipeline
- [ ] Load testing completed
- [ ] Monitoring setup

### Short-term (3-6 months)
- [ ] Horizontal scaling tested
- [ ] Read replicas for read-heavy services
- [ ] Redis cluster setup
- [ ] Comprehensive monitoring
- [ ] Automated alerting

### Medium-term (6-12 months)
- [ ] Kubernetes migration
- [ ] Multi-region deployment
- [ ] Database read replicas
- [ ] Advanced caching strategies
- [ ] Performance optimization

### Long-term (1-5 years)
- [ ] Database sharding
- [ ] Microservices further decomposition
- [ ] Event sourcing for critical services
- [ ] Global CDN
- [ ] AI-powered scaling

## 10. Risk Mitigation

### 10.1 Single Points of Failure

- **RabbitMQ**: Use cluster (3+ nodes)
- **SQL Server**: Use Always On availability groups
- **Redis**: Use cluster mode
- **ApiGateway**: Multiple instances behind load balancer

### 10.2 Data Consistency

- **Eventual Consistency**: Acceptable for most operations
- **Strong Consistency**: Required for financial transactions (orders)
- **Saga Pattern**: For distributed transactions

### 10.3 Disaster Recovery

- **Backup Strategy**: Daily backups, 7-day retention
- **Recovery Time Objective (RTO)**: 4 hours
- **Recovery Point Objective (RPO)**: 1 hour
- **Multi-Region**: Year 2+ goal

## Conclusion

This scaling strategy provides a roadmap for growing the Georgia Tech Library Marketplace from a small project to a global platform. The event-driven microservices architecture with separate databases per service provides the flexibility needed for independent scaling of each component.

Key principles:
1. **Start simple**: Current architecture supports initial growth
2. **Scale incrementally**: Add capacity as needed
3. **Monitor closely**: Use metrics to guide scaling decisions
4. **Automate everything**: CI/CD, deployment, scaling
5. **Plan for growth**: Architecture supports 5-year vision

