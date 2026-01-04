# Technologies & Tools

## 🛠️ Technology Stack

### Backend Framework
**ASP.NET Core 10.0**
- **Why:** Latest .NET version, high performance, cross-platform
- **What:** Web framework for building REST APIs
- **How Used:**
  - All 6 microservices built as ASP.NET Core Web APIs
  - Minimal API approach with controllers
  - Built-in dependency injection
  - Middleware pipeline for auth, CORS, rate limiting

### Programming Language
**C# 13**
- **Why:** Strong typing, modern features, excellent tooling
- **Features Used:**
  - Record types for DTOs
  - Nullable reference types for safety
  - Pattern matching
  - Async/await throughout
  - Primary constructors (C# 12+)

### Database
**Microsoft SQL Server 2022**
- **Why:** Enterprise-grade, ACID compliant, excellent with .NET
- **What:** Relational database for persistent storage
- **How Used:**
  - Primary data store for all entities
  - Tables: Users, Particles, PersonalityMetrics, DailyInputs, UniverseStates, ParticleEvents
  - Indexes on frequently queried columns (UserId, ParticleId, CreatedAt)
  - Stored procedures for complex queries (spatial queries)
  - Connection pooling via Microsoft.Data.SqlClient

**Configuration:**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=PersonalUniverse;User Id=sa;Password=***;TrustServerCertificate=true"
  }
}
```

### Data Access
**Dapper 2.1.52**
- **Why:** Lightweight micro-ORM, raw performance, SQL control
- **What:** Object mapper that wraps ADO.NET
- **How Used:**
  - All repositories use Dapper for CRUD operations
  - Parameterized queries prevent SQL injection
  - Manual mapping for complex objects
  - Async operations (QueryAsync, ExecuteAsync)

**Example:**
```csharp
public async Task<Particle?> GetByIdAsync(Guid id, CancellationToken ct)
{
    using var connection = _connectionFactory.CreateConnection();
    var sql = "SELECT * FROM Particles WHERE Id = @Id";
    return await connection.QueryFirstOrDefaultAsync<Particle>(sql, new { Id = id });
}
```

**vs Entity Framework Core:**
- Dapper: Faster, more control, less magic
- EF Core: Better for complex relationships, migrations, change tracking
- Choice: Dapper for read-heavy microservices with simple queries

### Caching
**Redis 7.4 (via StackExchange.Redis 2.10.1)**
- **Why:** In-memory data structure store, blazing fast, distributed caching
- **What:** Key-value cache with TTL support
- **How Used:**
  - Cache active particles (5-minute TTL)
  - Cache individual particles (15-minute TTL)
  - Cache personality metrics (1-hour TTL)
  - Cache-aside pattern: Check cache → If miss → Query DB → Store in cache

**Implementation:**
```csharp
public async Task<IEnumerable<Particle>> GetActiveParticlesAsync(CancellationToken ct)
{
    // Try cache first
    var cached = await _cacheService.GetActiveParticlesAsync(ct);
    if (cached.Any()) return cached;
    
    // Cache miss - query database
    var particles = await _particleRepository.GetActiveParticlesAsync(ct);
    
    // Store in cache
    if (particles.Any())
    {
        await _cacheService.SetActiveParticlesAsync(particles, ct);
    }
    
    return particles;
}
```

**Configuration:**
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

### Message Broker
**RabbitMQ 3.13 (via RabbitMQ.Client 6.8.1)**
- **Why:** Reliable message queueing, pub/sub pattern, guaranteed delivery
- **What:** Message broker for async communication
- **How Used:**
  - Event-driven architecture
  - Topic exchange: `personaluniverse.events`
  - Routing keys: `particle.*`, `personality.*`
  - Persistent messages (survive broker restart)
  - Durable queues

**Event Types Published:**
- `particle.spawned` - New particle created
- `particle.merged` - Two particles combined
- `particle.repelled` - Particles pushed apart
- `particle.expired` - Particle reached end of life
- `particle.interaction` - Any particle interaction
- `personality.updated` - User personality metrics changed

**Publisher Implementation:**
```csharp
public async Task PublishAsync<T>(string routingKey, T @event) where T : class
{
    var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));
    
    var properties = _channel.CreateBasicProperties();
    properties.Persistent = true; // Survive restarts
    properties.ContentType = "application/json";
    
    _channel.BasicPublish(
        exchange: _settings.ExchangeName,
        routingKey: routingKey,
        basicProperties: properties,
        body: body
    );
}
```

**Subscriber Implementation:**
```csharp
public async Task SubscribeAsync<T>(string routingKey, Func<T, Task> handler)
{
    var queueName = $"queue.{routingKey}";
    
    _channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
    _channel.QueueBind(queueName, _settings.ExchangeName, routingKey);
    
    var consumer = new AsyncEventingBasicConsumer(_channel);
    consumer.Received += async (sender, args) =>
    {
        var body = Encoding.UTF8.GetString(args.Body.ToArray());
        var @event = JsonSerializer.Deserialize<T>(body);
        await handler(@event);
        _channel.BasicAck(args.DeliveryTag, multiple: false);
    };
    
    _channel.BasicConsume(queueName, autoAck: false, consumer);
}
```

### Background Jobs
**Hangfire 1.8.22**
- **Why:** Reliable job scheduling, retry logic, dashboard
- **What:** Background job processor for .NET
- **How Used:**
  - Daily universe tick (cron: `0 0 * * *` - midnight UTC)
  - Particle decay check (cron: `0 */6 * * *` - every 6 hours)
  - Expired particle cleanup (cron: `0 1 * * *` - 1 AM daily)
  - SQL Server storage for job persistence
  - Dashboard at `/hangfire` for monitoring

**Job Registration:**
```csharp
// In Program.cs
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    
    recurringJobManager.AddOrUpdate<SimulationJobs>(
        "daily-universe-tick",
        job => job.ProcessDailyTickAsync(),
        "0 0 * * *", // Midnight UTC
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc }
    );
}
```

**Job Implementation:**
```csharp
public class SimulationJobs
{
    private readonly ISimulationService _simulationService;
    private readonly ILogger<SimulationJobs> _logger;
    
    public SimulationJobs(ISimulationService simulationService, ILogger<SimulationJobs> logger)
    {
        _simulationService = simulationService;
        _logger = logger;
    }
    
    public async Task ProcessDailyTickAsync()
    {
        _logger.LogInformation("Starting daily simulation tick");
        await _simulationService.ProcessSimulationTickAsync();
        _logger.LogInformation("Completed daily simulation tick");
    }
}
```

**Hangfire Configuration:**
```csharp
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    })
);

builder.Services.AddHangfireServer();
```

### Real-Time Communication
**SignalR (ASP.NET Core SignalR)**
- **Why:** Real-time bidirectional communication, WebSocket support
- **What:** Library for server-to-client push
- **How Used:**
  - UniverseHub at `/universehub`
  - Broadcasting particle state updates
  - Client subscription to specific particles
  - Universe-wide broadcasts
  - Automatic fallback (WebSocket → SSE → Long Polling)

**Hub Implementation:**
```csharp
public class UniverseHub : Hub
{
    public async Task JoinUniverse(string universeId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"universe:{universeId}");
    }
    
    public async Task FollowParticle(string particleId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"particle:{particleId}");
    }
}
```

**Broadcasting Service:**
```csharp
public class UniverseBroadcastService
{
    private readonly IHubContext<UniverseHub> _hubContext;
    
    public async Task BroadcastParticleUpdateAsync(ParticleDto particle)
    {
        await _hubContext.Clients.Group($"universe:{particle.UniverseId}")
            .SendAsync("ParticleUpdated", particle);
    }
}
```

### Authentication
**JWT (JSON Web Tokens)**
- **Why:** Stateless, scalable, standard
- **What:** Token-based authentication
- **How Used:**
  - Generated on login/registration
  - Signed with HMAC-SHA256
  - Contains claims (UserId, Email, Issuer, Audience, Expiration)
  - Validated on every protected endpoint
  - Stored client-side (localStorage or cookie)

**JWT Generation:**
```csharp
public string GenerateToken(Guid userId, string email)
{
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim(ClaimTypes.Email, email),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };
    
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"]));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    
    var token = new JwtSecurityToken(
        issuer: _configuration["Jwt:Issuer"],
        audience: _configuration["Jwt:Audience"],
        claims: claims,
        expires: DateTime.UtcNow.AddHours(24),
        signingCredentials: creds
    );
    
    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

**JWT Validation:**
```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
        };
    });
```

**Google OAuth 2.0 (via Google.Apis.Auth 1.69.0)**
- **Why:** Trusted third-party auth, no password storage, better UX
- **What:** OAuth 2.0 provider integration
- **How Used:**
  - Client-side: User clicks "Sign in with Google"
  - Google redirects with ID token
  - Server validates token with Google
  - Creates/fetches user from database
  - Issues JWT for our system

**Google Token Validation:**
```csharp
public async Task<GoogleAuthResult> ValidateGoogleTokenAsync(string idToken)
{
    var settings = new GoogleJsonWebSignature.ValidationSettings
    {
        Audience = new[] { _configuration["Authentication:Google:ClientId"] }
    };
    
    var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
    
    return new GoogleAuthResult
    {
        Email = payload.Email,
        Name = payload.Name,
        PictureUrl = payload.Picture,
        GoogleId = payload.Subject
    };
}
```

### Password Hashing
**BCrypt.Net-Next 4.0.3**
- **Why:** Industry standard, slow by design (anti-brute-force), salted automatically
- **What:** Password hashing algorithm
- **How Used:**
  - Hash passwords on registration
  - Verify passwords on login
  - Never store plaintext passwords

**Implementation:**
```csharp
// Registration
string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

// Login
bool isValid = BCrypt.Net.BCrypt.Verify(password, storedHash);
```

### Rate Limiting
**System.Threading.RateLimiting (ASP.NET Core 10.0)**
- **Why:** Built-in, performant, multiple algorithms
- **What:** Middleware for request throttling
- **How Used:**
  - Fixed window algorithm
  - Per-user partitioning (authenticated)
  - Per-IP partitioning (anonymous)
  - Different limits per service

**Configuration:**
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10, // 10 requests
                Window = TimeSpan.FromMinutes(1) // per minute
            }
        );
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
```

### API Documentation
**Scalar.AspNetCore 2.11.10**
- **Why:** Modern, interactive, better than Swagger UI
- **What:** OpenAPI/Swagger documentation UI
- **How Used:**
  - Auto-generates from controllers
  - Available at `/scalar/v1` on each service
  - Supports "Try it out" feature
  - Shows request/response schemas

**Configuration:**
```csharp
builder.Services.AddOpenApi(); // Generate OpenAPI spec

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // Serve OpenAPI JSON
    app.MapScalarApiReference(); // Serve Scalar UI
}
```

### CORS (Cross-Origin Resource Sharing)
**Microsoft.AspNetCore.Cors (built-in)**
- **Why:** Allow frontend (different origin) to call APIs
- **What:** HTTP header-based security
- **How Used:**
  - AllowAnyOrigin (development only)
  - AllowAnyMethod (GET, POST, PUT, DELETE)
  - AllowAnyHeader
  - Production: Restrict to specific origins

**Configuration:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

app.UseCors("AllowAll");
```

### Logging
**Microsoft.Extensions.Logging (built-in)**
- **Why:** Structured logging, abstraction over providers
- **What:** Logging framework
- **How Used:**
  - ILogger<T> injected into services
  - Log levels: Debug, Information, Warning, Error, Critical
  - Includes context (timestamp, log level, category, message)

**Example:**
```csharp
_logger.LogInformation("Spawned particle {ParticleId} for user {UserId}", particleId, userId);
_logger.LogError(ex, "Error processing daily tick");
```

### Containerization
**Docker & Docker Compose 27.x**
- **Why:** Consistent environments, easy deployment, isolation
- **What:** Container platform
- **How Used:**
  - Each service runs in its own container
  - docker-compose.yml orchestrates all services
  - Volumes for data persistence
  - Networks for service communication
  - Environment variables for configuration

**docker-compose.yml structure:**
```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    ports: ["1433:1433"]
    volumes: ["sqldata:/var/opt/mssql"]
  
  rabbitmq:
    image: rabbitmq:3.13-management
    ports: ["5672:5672", "15672:15672"]
  
  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]
  
  identity-service:
    build: ./src/Services/Identity
    ports: ["5001:8080"]
    depends_on: [sqlserver, redis]
    environment:
      - ConnectionStrings__DefaultConnection=${SQL_CONNECTION_STRING}
      - Jwt__SecretKey=${JWT_SECRET_KEY}
```

### HTTP Client
**HttpClient (System.Net.Http)**
- **Why:** Inter-service communication
- **What:** HTTP client for REST calls
- **How Used:**
  - Identity → Simulation Engine (spawn particle)
  - Visualization Feed → Simulation Engine (fetch particles)
  - Configured with HttpClientFactory for pooling

**Configuration:**
```csharp
builder.Services.AddHttpClient();

// Usage
public class AuthenticationService
{
    private readonly HttpClient _httpClient;
    
    public AuthenticationService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }
    
    public async Task SpawnParticleAsync(Guid userId)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "http://localhost:5004/api/particles/spawn",
            new { UserId = userId }
        );
        response.EnsureSuccessStatusCode();
    }
}
```

## 📦 NuGet Package Summary

### Shared Across All Services
- **Microsoft.AspNetCore.OpenApi** 10.0.1 - OpenAPI spec generation
- **Scalar.AspNetCore** 2.11.10 - API documentation UI

### Identity Service Specific
- **Microsoft.AspNetCore.Authentication.JwtBearer** 10.0.1
- **Google.Apis.Auth** 1.69.0
- **BCrypt.Net-Next** 4.0.3

### Storage Service Specific
- **Dapper** 2.1.52
- **Microsoft.Data.SqlClient** 6.0.1

### Simulation Engine Specific
- **Hangfire.AspNetCore** 1.8.22
- **Hangfire.Core** 1.8.22
- **Hangfire.SqlServer** 1.8.22
- **StackExchange.Redis** 2.10.1
- **RabbitMQ.Client** 6.8.1

### Event Service Specific
- **RabbitMQ.Client** 6.8.1

### Personality Processing Specific
- **System.Threading.RateLimiting** (built-in .NET 10)

### Visualization Feed Specific
- **Microsoft.AspNetCore.SignalR** (built-in)

## 🔧 Development Tools

### IDE
**Visual Studio Code / Visual Studio 2022**
- C# extension with IntelliSense
- REST Client extension for API testing
- Docker extension for container management

### Version Control
**Git + GitHub**
- Source control
- Issue tracking
- Documentation hosting

### Database Management
**Azure Data Studio / SQL Server Management Studio**
- Query execution
- Schema design
- Performance analysis

### API Testing
**Postman / HTTP files**
- Endpoint testing
- Environment variables
- Collection sharing

### Container Management
**Docker Desktop**
- Container orchestration
- Volume management
- Network inspection

## 🌐 Hosting/Deployment Options

### Cloud Providers (Planned)
- **Azure:** App Services, SQL Database, SignalR Service, Container Apps
- **AWS:** ECS, RDS, ElastiCache, API Gateway
- **Google Cloud:** Cloud Run, Cloud SQL, Memorystore

### Current Setup
- **Local Development:** Docker Compose
- **Database:** SQL Server 2022 (containerized)
- **Message Broker:** RabbitMQ (containerized)
- **Cache:** Redis (containerized)
