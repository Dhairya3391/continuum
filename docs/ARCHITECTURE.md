# System Architecture

## 🏛️ High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              EXTERNAL SERVICES                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │ Google OAuth │  │  SQL Server  │  │   RabbitMQ   │  │    Redis     │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
         │                  │                  │                  │
         ▼                  ▼                  ▼                  ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          MICROSERVICES LAYER                                │
│                                                                             │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐              │
│  │   Identity     │  │    Storage     │  │  Personality   │              │
│  │   Service      │  │    Service     │  │  Processing    │              │
│  │   :5001        │  │   :5002        │  │   :5003        │              │
│  │ ┌────────────┐ │  │ ┌────────────┐ │  │ ┌────────────┐ │              │
│  │ │  JWT Auth  │ │  │ │  Dapper    │ │  │ │ Sentiment  │ │              │
│  │ │  Google    │ │  │ │  Repos     │ │  │ │ Analysis   │ │              │
│  │ │  OAuth     │ │  │ │  SQL Conn  │ │  │ │ Metrics    │ │              │
│  │ └────────────┘ │  │ └────────────┘ │  │ └────────────┘ │              │
│  └────────────────┘  └────────────────┘  └────────────────┘              │
│                                                                             │
│  ┌────────────────┐  ┌────────────────┐  ┌────────────────┐              │
│  │  Simulation    │  │     Event      │  │ Visualization  │              │
│  │    Engine      │  │    Service     │  │     Feed       │              │
│  │    :5004       │  │    :5005       │  │    :5006       │              │
│  │ ┌────────────┐ │  │ ┌────────────┐ │  │ ┌────────────┐ │              │
│  │ │  Physics   │ │  │ │  RabbitMQ  │ │  │ │  SignalR   │ │              │
│  │ │  Hangfire  │ │  │ │  Pub/Sub   │ │  │ │   Hubs     │ │              │
│  │ │  Redis     │ │  │ │  Consumer  │ │  │ │  Broadcast │ │              │
│  │ └────────────┘ │  │ └────────────┘ │  │ └────────────┘ │              │
│  └────────────────┘  └────────────────┘  └────────────────┘              │
└─────────────────────────────────────────────────────────────────────────────┘
         │                                            │
         ▼                                            ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          SHARED LIBRARIES                                   │
│  ┌────────────────────────────────┐  ┌────────────────────────────────┐   │
│  │ PersonalUniverse.Shared.Models │  │PersonalUniverse.Shared.Contracts│   │
│  │  • Entities (User, Particle)   │  │  • Interfaces (IRepository)     │   │
│  │  • DTOs (ParticleDto, etc.)    │  │  • Events (ParticleSpawned)     │   │
│  │  • Mappers (ParticleMapper)    │  │  • Enums (ParticleState)        │   │
│  └────────────────────────────────┘  └────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

## 🔄 Communication Patterns

### 1. Synchronous Communication (REST API)
```
Client/Service → HTTP Request → Target Service → HTTP Response
```

**Examples:**
- Identity → Simulation Engine (spawn particle after registration)
- Personality Processing → Storage Service (save daily input)
- Visualization Feed → Simulation Engine (fetch active particles)

**Characteristics:**
- Request-response pattern
- Blocking operation
- Immediate feedback
- Used for critical path operations

### 2. Asynchronous Communication (RabbitMQ)
```
Publisher → RabbitMQ Exchange → Queue → Subscriber → Event Handler
```

**Examples:**
- Simulation Engine publishes `ParticleSpawnedEvent`
- Event Service consumes and logs event
- Multiple subscribers can process same event

**Characteristics:**
- Fire-and-forget pattern
- Non-blocking
- Guaranteed delivery (persistent messages)
- Topic-based routing (particle.*, personality.*)

### 3. Real-time Push (SignalR)
```
Server → SignalR Hub → WebSocket → Connected Clients
```

**Examples:**
- Simulation Engine → Visualization Feed → All connected browsers
- Particle state updates
- Interaction notifications

**Characteristics:**
- Server-initiated push
- Bidirectional communication
- Low latency
- Connection-based (WebSocket/Long Polling)

## 📐 Architectural Patterns

### 1. Microservices Architecture
**Why:** Separation of concerns, independent scaling, technology diversity

**Implementation:**
- Each service is independently deployable
- Services communicate via REST APIs and message queues
- Shared libraries minimize code duplication
- Each service has its own port and can be scaled independently

### 2. Repository Pattern
**Why:** Abstract data access, testability, swappable data sources

**Implementation:**
```csharp
IParticleRepository (Interface in Shared.Contracts)
    ↓
ParticleRepository (Implementation in Storage.API)
    ↓
Dapper (Micro-ORM)
    ↓
SQL Server
```

All data access goes through repositories, making it easy to:
- Mock repositories for testing
- Change ORM or database without affecting services
- Add caching layer transparently

### 3. Mapper Pattern
**Why:** Separate domain entities from DTOs, protect internal models

**Implementation:**
```csharp
Particle (Entity) → ParticleMapper.ToDto() → ParticleDto (DTO)
```

Benefits:
- Entities contain business logic
- DTOs are for transport only
- Mapping logic is centralized
- Easy to add validation or transformation

### 4. Event-Driven Architecture
**Why:** Loose coupling, async processing, event sourcing

**Implementation:**
```
Action → Event Published → Event Stored → Event Consumed → Side Effects
```

Example flow:
1. Particles merge in Simulation Engine
2. `ParticleMergedEvent` published to RabbitMQ
3. Event Service logs event to database
4. Visualization Feed broadcasts to clients
5. Analytics service could consume for metrics (future)

### 5. CQRS-Lite (Command Query Responsibility Segregation)
**Why:** Optimize reads separately from writes

**Implementation:**
- **Writes:** Direct to SQL Server via repositories
- **Reads:** Check Redis cache first, fallback to database
- Active particles cached for 5 minutes
- Individual particles cached for 15 minutes

Benefits:
- Faster read performance
- Reduced database load
- Stale data is acceptable for visualization

### 6. Background Job Pattern
**Why:** Long-running tasks, scheduled operations, retry logic

**Implementation:**
```
Hangfire Scheduler → Job Queue → Job Executor → Business Logic
```

Jobs:
- **Daily Universe Tick** (Midnight UTC) - Process all particle movements
- **Decay Check** (Every 6 hours) - Mark inactive particles
- **Cleanup** (Daily) - Remove expired particles older than 30 days

## 🔐 Security Architecture

### Authentication Flow
```
┌──────────┐                           ┌──────────────┐
│  Client  │                           │   Identity   │
│          │                           │   Service    │
└──────────┘                           └──────────────┘
     │                                        │
     │  1. POST /api/auth/register           │
     │  (email, password)                    │
     ├──────────────────────────────────────>│
     │                                        │ 2. Hash password (BCrypt)
     │                                        │ 3. Save to database
     │                                        │ 4. Generate JWT
     │                                        │
     │  5. Response: { token, userId }       │
     │<──────────────────────────────────────┤
     │                                        │
     │  6. Subsequent requests with          │
     │  Header: Authorization: Bearer <JWT>  │
     ├──────────────────────────────────────>│
     │                                        │ 7. Validate JWT signature
     │                                        │ 8. Check expiration
     │                                        │ 9. Extract claims
     │                                        │
```

### Google OAuth Flow
```
┌──────────┐         ┌──────────────┐         ┌────────────┐
│  Client  │         │   Identity   │         │   Google   │
│          │         │   Service    │         │   OAuth    │
└──────────┘         └──────────────┘         └────────────┘
     │                      │                       │
     │ 1. User clicks      │                       │
     │    "Sign in with    │                       │
     │     Google"         │                       │
     │                     │                       │
     │ 2. Redirect to      │                       │
     │    Google OAuth     │                       │
     ├─────────────────────┼──────────────────────>│
     │                     │                       │ 3. User authenticates
     │                     │                       │    with Google
     │                     │                       │
     │ 4. Google redirects │                       │
     │    with ID token    │                       │
     │<────────────────────┼───────────────────────┤
     │                     │                       │
     │ 5. POST /api/auth/google                   │
     │    { idToken }      │                       │
     ├────────────────────>│                       │
     │                     │ 6. Validate token     │
     │                     ├──────────────────────>│
     │                     │                       │
     │                     │ 7. Token valid        │
     │                     │<──────────────────────┤
     │                     │                       │
     │                     │ 8. Get/create user    │
     │                     │ 9. Generate JWT       │
     │                     │                       │
     │ 10. Response        │                       │
     │     { token, userId}│                       │
     │<────────────────────┤                       │
```

### Rate Limiting Layers

**Layer 1: Application-Level (Business Logic)**
```csharp
// In PersonalityProcessingService
var today = DateTime.UtcNow.Date;
var todayInputCount = await _inputRepo.GetCountByUserAndDateAsync(userId, today);

if (todayInputCount >= 3)
{
    throw new InvalidOperationException("Daily input limit reached");
}
```

**Layer 2: Middleware-Level (ASP.NET)**
```csharp
// In Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        context => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }
        )
    );
});
```

**Layer 3: Infrastructure-Level (Future: API Gateway)**
- Could add Kong, Nginx, or Azure API Management
- IP-based throttling
- DDoS protection

## 📊 Data Flow Diagrams

### Daily Input Processing Flow
```
┌────────┐   1. Submit Input    ┌─────────────────┐
│ Client │ ─────────────────────>│  Personality    │
│        │                       │  Processing     │
└────────┘                       │  Service        │
                                 └─────────────────┘
                                         │
                    2. Check rate limit  │
                    (3 per day)          │
                                         ▼
                                 ┌─────────────────┐
                                 │  Daily Input    │
                                 │  Repository     │
                                 └─────────────────┘
                                         │
                    3. Analyze sentiment │
                                         ▼
                                 ┌─────────────────┐
                                 │  Personality    │
                                 │  Metrics Calc   │
                                 └─────────────────┘
                                         │
                    4. Save metrics      │
                                         ▼
                                 ┌─────────────────┐
                                 │  Personality    │
                                 │  Metrics Repo   │
                                 └─────────────────┘
                                         │
                    5. Update particle   │
                    (LastInputAt)        │
                                         ▼
                                 ┌─────────────────┐
                                 │   Particle      │
                                 │   Repository    │
                                 └─────────────────┘
```

### Daily Simulation Tick Flow
```
┌─────────────┐   Midnight UTC   ┌─────────────────┐
│  Hangfire   │ ──────────────────>│  Simulation     │
│  Scheduler  │                    │  Jobs           │
└─────────────┘                    │  (Daily Tick)   │
                                   └─────────────────┘
                                           │
                      1. Get all active    │
                         particles         │
                                           ▼
                                   ┌─────────────────┐
                                   │   Particle      │
                                   │   Repository    │
                                   └─────────────────┘
                                           │
                      2. For each particle │
                         apply physics     │
                                           ▼
                                   ┌─────────────────┐
                                   │  Simulation     │
                                   │  Service        │
                                   └─────────────────┘
                                           │
                      3. Check neighbors   │
                         in radius 50      │
                                           ▼
                                   ┌─────────────────┐
                                   │  Interaction    │
                                   │  Service        │
                                   └─────────────────┘
                                           │
                      4. Apply interaction │
                         (merge/repel/etc) │
                                           ▼
                                   ┌─────────────────┐
                                   │  Update         │
                                   │  Particles      │
                                   └─────────────────┘
                                           │
                      5. Publish events    │
                                           ▼
                                   ┌─────────────────┐
                                   │   RabbitMQ      │
                                   │   Publisher     │
                                   └─────────────────┘
                                           │
                      6. Broadcast state   │
                                           ▼
                                   ┌─────────────────┐
                                   │  Visualization  │
                                   │  Feed (SignalR) │
                                   └─────────────────┘
```

### Particle Interaction Decision Tree
```
Two particles within 50 units
        │
        ▼
Get personality metrics for both
        │
        ▼
Calculate compatibility score
        │
        ├─── > 0.85 ──────> MERGE
        │                   • Combine particles
        │                   • Sum mass & energy
        │                   • Average traits
        │
        ├─── 0.65-0.85 ───> BOND
        │                   • Align velocities
        │                   • Mutual influence
        │
        ├─── 0.50-0.65 ───> ATTRACT
        │                   • Gravitational pull
        │                   • Gradual approach
        │
        └─── < 0.50 ──────> REPEL
                            • Opposing force
                            • Push apart
```

## 🎯 Scalability Considerations

### Current Design (Single Instance)
- All services on same machine
- One SQL Server database
- One RabbitMQ instance
- One Redis instance

### Horizontal Scaling Path
1. **Database:** Master-slave replication, read replicas
2. **Services:** Run multiple instances behind load balancer
3. **RabbitMQ:** Cluster with mirrored queues
4. **Redis:** Redis Cluster or Sentinel for HA
5. **Hangfire:** Multiple servers sharing same job storage

### Performance Bottlenecks & Solutions
| Bottleneck | Solution |
|------------|----------|
| SQL queries | Add Redis caching layer ✅ |
| Daily tick processing | Partition universe, parallel processing |
| Event throughput | RabbitMQ clustering, batch processing |
| SignalR connections | Redis backplane for multi-server |
| Hangfire job queue | Dedicated worker servers |

## 🔍 Monitoring Points

### Health Checks
- Each service: `/health` endpoint
- Database connection
- RabbitMQ connection
- Redis connection

### Key Metrics to Monitor
- **API:** Request rate, latency, error rate
- **Database:** Query time, connection pool, deadlocks
- **Cache:** Hit rate, memory usage, eviction rate
- **Events:** Publish rate, consume lag, failed messages
- **Jobs:** Execution time, failure rate, queue depth
- **Particles:** Active count, interactions per tick, decay rate

### Logging Strategy
- **Structured logging** with Serilog
- **Log levels:** Debug, Info, Warning, Error, Fatal
- **Context:** CorrelationId, UserId, ParticleId
- **Destinations:** Console (dev), File (prod), Seq (future)
