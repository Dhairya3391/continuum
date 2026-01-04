# Microservices (Continued)

## 4. Simulation Engine (Port 5004)

### Purpose
Core of the simulation. Handles particle physics, interactions, and daily processing. Manages Redis caching. Runs Hangfire background jobs.

### Responsibilities
- Particle spawning and lifecycle
- Physics calculations (position, velocity, energy)
- Neighbor detection within interaction radius
- Particle interactions (merge, bond, attract, repel)
- Daily simulation tick processing
- Decay system (inactive particles)
- Event publishing to RabbitMQ
- Real-time state caching in Redis
- Background job scheduling

### Technology Stack
- **ASP.NET Core 10.0** - Web API framework
- **Hangfire 1.8.22** - Background job scheduler
- **StackExchange.Redis 2.10.1** - Redis client
- **RabbitMQ.Client 6.8.1** - Event publishing

### API Endpoints

#### POST /api/particles/spawn
**Purpose:** Create new particle for user

**Request:**
```json
{
  "userId": "guid"
}
```

**Response:**
```json
{
  "id": "guid",
  "userId": "guid",
  "positionX": 342.56,
  "positionY": 789.23,
  "velocityX": 0.0,
  "velocityY": 0.0,
  "mass": 1.0,
  "energy": 100.0,
  "state": "Active",
  "createdAt": "2026-01-03T10:30:00Z",
  "lastUpdatedAt": "2026-01-03T10:30:00Z",
  "lastInputAt": "2026-01-03T10:30:00Z",
  "decayLevel": 0
}
```

**Implementation:**
```csharp
public async Task<ParticleDto> SpawnParticleAsync(Guid userId, CancellationToken ct)
{
    // Check if particle already exists
    var existing = await _particleRepo.GetByUserIdAsync(userId, ct);
    if (existing != null)
    {
        return ParticleMapper.ToDto(existing);
    }
    
    // Create new particle at random position
    var particle = new Particle
    {
        UserId = userId,
        PositionX = Random.Shared.NextDouble() * 1000, // Universe is 1000x1000
        PositionY = Random.Shared.NextDouble() * 1000,
        VelocityX = 0,
        VelocityY = 0,
        Mass = 1.0,
        Energy = 100.0,
        State = ParticleState.Active,
        CreatedAt = DateTime.UtcNow,
        LastUpdatedAt = DateTime.UtcNow,
        LastInputAt = DateTime.UtcNow,
        DecayLevel = 0
    };
    
    var particleId = await _particleRepo.AddAsync(particle, ct);
    particle.Id = particleId;
    
    // Initialize default personality metrics
    var metrics = new PersonalityMetrics
    {
        ParticleId = particleId,
        Curiosity = 0.5,
        SocialAffinity = 0.5,
        Aggression = 0.5,
        Stability = 0.5,
        GrowthPotential = 0.5,
        CalculatedAt = DateTime.UtcNow,
        Version = 1
    };
    await _metricsRepo.AddAsync(metrics, ct);
    
    // Cache particle
    await _cacheService.CacheParticleAsync(particle, ct);
    await _cacheService.CachePersonalityMetricsAsync(metrics, ct);
    
    // Publish event
    await _eventPublisher.PublishAsync("particle.spawned", new ParticleSpawnedEvent
    {
        ParticleId = particleId,
        UserId = userId,
        InitialX = particle.PositionX,
        InitialY = particle.PositionY,
        Timestamp = DateTime.UtcNow
    });
    
    return ParticleMapper.ToDto(particle);
}
```

#### GET /api/particles/active
**Purpose:** Get all active particles

**Response:**
```json
[
  {
    "id": "guid",
    "userId": "guid",
    "positionX": 342.56,
    "positionY": 789.23,
    "velocityX": 1.5,
    "velocityY": -0.8,
    "mass": 1.2,
    "energy": 95.5,
    "state": "Active",
    ...
  },
  ...
]
```

**Caching Strategy:**
```csharp
public async Task<IEnumerable<ParticleDto>> GetActiveParticlesAsync(CancellationToken ct)
{
    // Try cache first (5-minute TTL)
    var cachedParticles = await _cacheService.GetActiveParticlesAsync(ct);
    if (cachedParticles.Any())
    {
        return ParticleMapper.ToDtos(cachedParticles);
    }
    
    // Cache miss - query database
    var particles = await _particleRepo.GetActiveParticlesAsync(ct);
    var particleList = particles.ToList();
    
    // Store in cache
    if (particleList.Any())
    {
        await _cacheService.SetActiveParticlesAsync(particleList, ct);
    }
    
    return ParticleMapper.ToDtos(particleList);
}
```

#### GET /api/particles/user/{userId}
**Purpose:** Get particle for specific user

#### PUT /api/particles/{id}
**Purpose:** Update particle state (usually called by simulation)

### Physics Engine

**Position Update (Toroidal Universe):**
```csharp
public void UpdatePosition(Particle particle, double deltaTime)
{
    // Apply velocity
    particle.PositionX += particle.VelocityX * deltaTime;
    particle.PositionY += particle.VelocityY * deltaTime;
    
    // Wrap around universe boundaries (1000 x 1000)
    particle.PositionX = (particle.PositionX + 1000) % 1000;
    particle.PositionY = (particle.PositionY + 1000) % 1000;
    
    // Energy decay from movement
    var speed = Math.Sqrt(particle.VelocityX * particle.VelocityX + particle.VelocityY * particle.VelocityY);
    particle.Energy -= speed * 0.01 * deltaTime;
    particle.Energy = Math.Max(particle.Energy, 0);
}
```

**Neighbor Detection:**
```csharp
public IEnumerable<Particle> FindNeighbors(Particle particle, IEnumerable<Particle> allParticles, double radius = 50.0)
{
    return allParticles.Where(p => 
        p.Id != particle.Id && 
        p.State == ParticleState.Active &&
        CalculateDistance(particle, p) <= radius
    );
}

private double CalculateDistance(Particle p1, Particle p2)
{
    var dx = p2.PositionX - p1.PositionX;
    var dy = p2.PositionY - p1.PositionY;
    
    // Account for toroidal wraparound
    if (Math.Abs(dx) > 500) dx = 1000 - Math.Abs(dx);
    if (Math.Abs(dy) > 500) dy = 1000 - Math.Abs(dy);
    
    return Math.Sqrt(dx * dx + dy * dy);
}
```

### Interaction System

**Compatibility Calculation:**
```csharp
public double CalculateCompatibility(PersonalityMetrics m1, PersonalityMetrics m2)
{
    // Weighted similarity scores
    var curiositySimilarity = 1.0 - Math.Abs(m1.Curiosity - m2.Curiosity);
    var socialSimilarity = 1.0 - Math.Abs(m1.SocialAffinity - m2.SocialAffinity);
    var aggressionDifference = Math.Abs(m1.Aggression - m2.Aggression); // Lower is better
    var stabilityAverage = (m1.Stability + m2.Stability) / 2.0;
    var growthSimilarity = 1.0 - Math.Abs(m1.GrowthPotential - m2.GrowthPotential);
    
    // Weighted combination
    var compatibility = 
        (curiositySimilarity * 0.20) +
        (socialSimilarity * 0.30) +
        ((1.0 - aggressionDifference) * 0.20) +
        (stabilityAverage * 0.15) +
        (growthSimilarity * 0.15);
    
    return Math.Clamp(compatibility, 0, 1);
}
```

**Interaction Decision:**
```csharp
public async Task ProcessInteractionAsync(Particle p1, Particle p2)
{
    var metrics1 = await _metricsRepo.GetLatestByParticleIdAsync(p1.Id);
    var metrics2 = await _metricsRepo.GetLatestByParticleIdAsync(p2.Id);
    
    var compatibility = CalculateCompatibility(metrics1, metrics2);
    
    if (compatibility > 0.85)
    {
        await MergeParticlesAsync(p1, p2);
    }
    else if (compatibility > 0.65)
    {
        await BondParticlesAsync(p1, p2);
    }
    else if (compatibility > 0.50)
    {
        await AttractParticlesAsync(p1, p2);
    }
    else
    {
        await RepelParticlesAsync(p1, p2);
    }
}
```

**Merge Implementation:**
```csharp
public async Task MergeParticlesAsync(Particle p1, Particle p2)
{
    // Combine properties
    var newMass = p1.Mass + p2.Mass;
    var newEnergy = p1.Energy + p2.Energy;
    
    // Weighted average for position and velocity
    p1.PositionX = (p1.PositionX * p1.Mass + p2.PositionX * p2.Mass) / newMass;
    p1.PositionY = (p1.PositionY * p1.Mass + p2.PositionY * p2.Mass) / newMass;
    p1.VelocityX = (p1.VelocityX * p1.Mass + p2.VelocityX * p2.Mass) / newMass;
    p1.VelocityY = (p1.VelocityY * p1.Mass + p2.VelocityY * p2.Mass) / newMass;
    
    p1.Mass = newMass;
    p1.Energy = newEnergy;
    p1.LastUpdatedAt = DateTime.UtcNow;
    
    // Mark second particle as merged
    p2.State = ParticleState.Merged;
    p2.LastUpdatedAt = DateTime.UtcNow;
    
    // Average personality traits
    var m1 = await _metricsRepo.GetLatestByParticleIdAsync(p1.Id);
    var m2 = await _metricsRepo.GetLatestByParticleIdAsync(p2.Id);
    
    var newMetrics = new PersonalityMetrics
    {
        ParticleId = p1.Id,
        Curiosity = (m1.Curiosity + m2.Curiosity) / 2,
        SocialAffinity = (m1.SocialAffinity + m2.SocialAffinity) / 2,
        Aggression = (m1.Aggression + m2.Aggression) / 2,
        Stability = Math.Min(m1.Stability + 0.1, 1.0), // Merging increases stability
        GrowthPotential = (m1.GrowthPotential + m2.GrowthPotential) / 2,
        CalculatedAt = DateTime.UtcNow,
        Version = Math.Max(m1.Version, m2.Version) + 1
    };
    
    await _particleRepo.UpdateAsync(p1);
    await _particleRepo.UpdateAsync(p2);
    await _metricsRepo.AddAsync(newMetrics);
    
    // Cache updates
    await _cacheService.CacheParticleAsync(p1);
    await _cacheService.InvalidateParticleAsync(p2.Id);
    
    // Publish event
    await _eventPublisher.PublishAsync("particle.merged", new ParticleMergedEvent
    {
        SourceParticleId = p2.Id,
        TargetParticleId = p1.Id,
        ResultingParticleId = p1.Id,
        NewMass = newMass,
        NewEnergy = newEnergy,
        Timestamp = DateTime.UtcNow
    });
}
```

**Repel Implementation:**
```csharp
public async Task RepelParticlesAsync(Particle p1, Particle p2)
{
    var dx = p2.PositionX - p1.PositionX;
    var dy = p2.PositionY - p1.PositionY;
    var distance = Math.Sqrt(dx * dx + dy * dy);
    
    if (distance < 0.1) return; // Avoid division by zero
    
    // Repulsion force inversely proportional to distance
    var forceMagnitude = 10.0 / (distance * distance);
    
    // Aggression increases repulsion force
    var m1 = await _metricsRepo.GetLatestByParticleIdAsync(p1.Id);
    var m2 = await _metricsRepo.GetLatestByParticleIdAsync(p2.Id);
    forceMagnitude *= (1 + m1.Aggression + m2.Aggression);
    
    // Normalize direction
    var forceX = -(dx / distance) * forceMagnitude;
    var forceY = -(dy / distance) * forceMagnitude;
    
    // Apply force (F = ma, assuming m=1 for simplicity, so a=F)
    p1.VelocityX += forceX;
    p1.VelocityY += forceY;
    p2.VelocityX -= forceX;
    p2.VelocityY -= forceY;
    
    // Clamp velocities
    var maxVelocity = 5.0;
    p1.VelocityX = Math.Clamp(p1.VelocityX, -maxVelocity, maxVelocity);
    p1.VelocityY = Math.Clamp(p1.VelocityY, -maxVelocity, maxVelocity);
    p2.VelocityX = Math.Clamp(p2.VelocityX, -maxVelocity, maxVelocity);
    p2.VelocityY = Math.Clamp(p2.VelocityY, -maxVelocity, maxVelocity);
    
    await _particleRepo.UpdateAsync(p1);
    await _particleRepo.UpdateAsync(p2);
    
    // Publish event
    await _eventPublisher.PublishAsync("particle.repelled", new ParticleRepelledEvent
    {
        Particle1Id = p1.Id,
        Particle2Id = p2.Id,
        RepulsionForce = forceMagnitude,
        Timestamp = DateTime.UtcNow
    });
}
```

### Daily Tick Processing

**Hangfire Job:**
```csharp
public class SimulationJobs
{
    private readonly ISimulationService _simulationService;
    private readonly ILogger<SimulationJobs> _logger;
    
    public async Task ProcessDailyTickAsync()
    {
        _logger.LogInformation("Starting daily simulation tick at {Time}", DateTime.UtcNow);
        await _simulationService.ProcessSimulationTickAsync();
        _logger.LogInformation("Completed daily simulation tick");
    }
}
```

**Simulation Tick Logic:**
```csharp
public async Task ProcessSimulationTickAsync()
{
    var particles = await _particleRepo.GetActiveParticlesAsync();
    var particleList = particles.ToList();
    
    // Phase 1: Update positions
    foreach (var particle in particleList)
    {
        UpdatePosition(particle, deltaTime: 1.0); // 1 day
        await _particleRepo.UpdateAsync(particle);
    }
    
    // Phase 2: Process interactions
    for (int i = 0; i < particleList.Count; i++)
    {
        for (int j = i + 1; j < particleList.Count; j++)
        {
            var p1 = particleList[i];
            var p2 = particleList[j];
            
            if (CalculateDistance(p1, p2) <= 50.0)
            {
                await ProcessInteractionAsync(p1, p2);
            }
        }
    }
    
    // Phase 3: Save universe state snapshot
    var snapshot = new UniverseState
    {
        TickNumber = await _stateRepo.GetNextTickNumberAsync(),
        ActiveParticleCount = particleList.Count,
        TotalEnergy = particleList.Sum(p => p.Energy),
        ProcessedAt = DateTime.UtcNow
    };
    await _stateRepo.AddAsync(snapshot);
    
    // Phase 4: Invalidate cache (force refresh)
    await _cacheService.RemoveAsync("simulation:active_particles");
    
    // Phase 5: Broadcast updates
    await _visualizationFeed.BroadcastUniverseStateAsync(particleList);
}
```

### Decay System

**Decay Check Job (Every 6 hours):**
```csharp
public async Task ProcessParticleDecayAsync()
{
    var particles = await _particleRepo.GetActiveParticlesAsync();
    var decayThreshold = DateTime.UtcNow.AddHours(-24);
    var decayedCount = 0;
    
    foreach (var particle in particles)
    {
        if (particle.LastInputAt < decayThreshold && particle.State == ParticleState.Active)
        {
            particle.State = ParticleState.Decaying;
            particle.DecayLevel += 10; // 10% per check
            
            if (particle.DecayLevel >= 100)
            {
                particle.State = ParticleState.Expired;
                
                // Publish expiration event
                await _eventPublisher.PublishAsync("particle.expired", new ParticleExpiredEvent
                {
                    ParticleId = particle.Id,
                    Reason = "Inactivity",
                    LastInputAt = particle.LastInputAt,
                    Timestamp = DateTime.UtcNow
                });
            }
            
            await _particleRepo.UpdateAsync(particle);
            decayedCount++;
        }
    }
    
    _logger.LogInformation("Processed decay for {Count} particles", decayedCount);
}
```

**Cleanup Job (Daily at 1 AM):**
```csharp
public async Task CleanupExpiredParticlesAsync()
{
    var allParticles = await _particleRepo.GetAllAsync();
    var expiredParticles = allParticles.Where(p => p.State == ParticleState.Expired);
    var cleanupThreshold = DateTime.UtcNow.AddDays(-30);
    var deletedCount = 0;
    
    foreach (var particle in expiredParticles)
    {
        if (particle.LastUpdatedAt < cleanupThreshold)
        {
            await _particleRepo.DeleteAsync(particle.Id);
            deletedCount++;
        }
    }
    
    _logger.LogInformation("Cleaned up {Count} expired particles", deletedCount);
}
```

### Hangfire Dashboard
Accessible at `/hangfire` on the Simulation Engine service.

**Authentication Filter:**
```csharp
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // Allow in development, require auth in production
        var httpContext = context.GetHttpContext();
        return httpContext.Request.Host.Host == "localhost" ||
               httpContext.User.Identity?.IsAuthenticated == true;
    }
}
```

### Redis Caching Service

**Cache Methods:**
```csharp
public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly TimeSpan _defaultExpiry = TimeSpan.FromMinutes(30);
    
    public async Task<IEnumerable<Particle>> GetActiveParticlesAsync(CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync("simulation:active_particles");
        
        if (value.IsNullOrEmpty)
            return Enumerable.Empty<Particle>();
        
        return JsonSerializer.Deserialize<IEnumerable<Particle>>(value!);
    }
    
    public async Task SetActiveParticlesAsync(IEnumerable<Particle> particles, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var serialized = JsonSerializer.Serialize(particles);
        await db.StringSetAsync("simulation:active_particles", serialized, TimeSpan.FromMinutes(5));
    }
    
    public async Task CacheParticleAsync(Particle particle, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var key = $"particle:{particle.Id}";
        var serialized = JsonSerializer.Serialize(particle);
        await db.StringSetAsync(key, serialized, TimeSpan.FromMinutes(15));
    }
}
```

---

## 5. Event Service (Port 5005)

### Purpose
Manages event publishing and consumption. Acts as bridge between RabbitMQ and other services. Logs all particle lifecycle events.

### Responsibilities
- Publish events to RabbitMQ
- Subscribe to events from RabbitMQ
- Event logging to database
- Background event consumer service
- Event replay (future feature)

### Technology Stack
- **ASP.NET Core 10.0** - Web API framework
- **RabbitMQ.Client 6.8.1** - Message broker client
- **Topic Exchange** - Event routing pattern

### Event Types

**ParticleSpawnedEvent:**
```csharp
public record ParticleSpawnedEvent
{
    public Guid ParticleId { get; init; }
    public Guid UserId { get; init; }
    public double InitialX { get; init; }
    public double InitialY { get; init; }
    public DateTime Timestamp { get; init; }
}
```

**ParticleMergedEvent:**
```csharp
public record ParticleMergedEvent
{
    public Guid SourceParticleId { get; init; }
    public Guid TargetParticleId { get; init; }
    public Guid ResultingParticleId { get; init; }
    public double NewMass { get; init; }
    public double NewEnergy { get; init; }
    public DateTime Timestamp { get; init; }
}
```

**ParticleRepelledEvent:**
```csharp
public record ParticleRepelledEvent
{
    public Guid Particle1Id { get; init; }
    public Guid Particle2Id { get; init; }
    public double RepulsionForce { get; init; }
    public DateTime Timestamp { get; init; }
}
```

**ParticleExpiredEvent:**
```csharp
public record ParticleExpiredEvent
{
    public Guid ParticleId { get; init; }
    public string Reason { get; init; }
    public DateTime LastInputAt { get; init; }
    public DateTime Timestamp { get; init; }
}
```

**ParticleInteractionEvent:**
```csharp
public record ParticleInteractionEvent
{
    public Guid Particle1Id { get; init; }
    public Guid Particle2Id { get; init; }
    public string InteractionType { get; init; } // Merge, Bond, Attract, Repel
    public double Compatibility { get; init; }
    public double ImpactStrength { get; init; }
    public DateTime Timestamp { get; init; }
}
```

**PersonalityUpdatedEvent:**
```csharp
public record PersonalityUpdatedEvent
{
    public Guid ParticleId { get; init; }
    public Guid UserId { get; init; }
    public int Version { get; init; }
    public PersonalityMetricsDto Metrics { get; init; }
    public DateTime Timestamp { get; init; }
}
```

### Event Publisher

```csharp
public class EventPublisher : IEventPublisher
{
    private readonly IModel _channel;
    private readonly RabbitMqSettings _settings;
    
    public EventPublisher(RabbitMqSettings settings)
    {
        _settings = settings;
        var factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password,
            VirtualHost = settings.VirtualHost
        };
        
        var connection = factory.CreateConnection();
        _channel = connection.CreateModel();
        
        // Declare exchange
        _channel.ExchangeDeclare(
            exchange: settings.ExchangeName,
            type: "topic",
            durable: true,
            autoDelete: false
        );
    }
    
    public async Task PublishAsync<T>(string routingKey, T @event) where T : class
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));
        
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        
        _channel.BasicPublish(
            exchange: _settings.ExchangeName,
            routingKey: routingKey,
            basicProperties: properties,
            body: body
        );
        
        await Task.CompletedTask;
    }
}
```

### Event Subscriber

```csharp
public class EventSubscriber : IEventSubscriber
{
    private readonly IModel _channel;
    private readonly RabbitMqSettings _settings;
    
    public EventSubscriber(RabbitMqSettings settings)
    {
        _settings = settings;
        var factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password,
            VirtualHost = settings.VirtualHost,
            DispatchConsumersAsync = true
        };
        
        var connection = factory.CreateConnection();
        _channel = connection.CreateModel();
        
        _channel.ExchangeDeclare(
            exchange: settings.ExchangeName,
            type: "topic",
            durable: true,
            autoDelete: false
        );
    }
    
    public async Task SubscribeAsync<T>(string routingKey, Func<T, Task> handler) where T : class
    {
        var queueName = $"queue.{routingKey.Replace("#", "all").Replace("*", "any")}";
        
        _channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false
        );
        
        _channel.QueueBind(
            queue: queueName,
            exchange: _settings.ExchangeName,
            routingKey: routingKey
        );
        
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (sender, args) =>
        {
            try
            {
                var body = Encoding.UTF8.GetString(args.Body.ToArray());
                var @event = JsonSerializer.Deserialize<T>(body);
                
                if (@event != null)
                {
                    await handler(@event);
                }
                
                _channel.BasicAck(args.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _channel.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
            }
        };
        
        _channel.BasicConsume(
            queue: queueName,
            autoAck: false,
            consumer: consumer
        );
        
        await Task.CompletedTask;
    }
}
```

### Background Event Consumer

```csharp
public class EventConsumerBackgroundService : BackgroundService
{
    private readonly IEventSubscriber _eventSubscriber;
    private readonly ILogger<EventConsumerBackgroundService> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Event Consumer Background Service starting...");
        
        // Subscribe to particle spawned events
        await _eventSubscriber.SubscribeAsync<ParticleSpawnedEvent>(
            "particle.spawned",
            async (@event) =>
            {
                _logger.LogInformation(
                    "Particle spawned: {ParticleId} for User: {UserId} at ({X}, {Y})",
                    @event.ParticleId, @event.UserId, @event.InitialX, @event.InitialY);
                
                // Could save to event log database here
            });
        
        // Subscribe to particle merged events
        await _eventSubscriber.SubscribeAsync<ParticleMergedEvent>(
            "particle.merged",
            async (@event) =>
            {
                _logger.LogInformation(
                    "Particles merged: {Source} + {Target} = {Result}",
                    @event.SourceParticleId, @event.TargetParticleId, @event.ResultingParticleId);
            });
        
        // Subscribe to all particle events with wildcard
        await _eventSubscriber.SubscribeAsync(
            "particle.#",
            async (message) =>
            {
                _logger.LogDebug("Received particle event: {Message}", message);
            });
        
        _logger.LogInformation("Event Consumer Background Service is listening...");
        
        // Keep service running
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
```

**Registered in Program.cs:**
```csharp
builder.Services.AddHostedService<EventConsumerBackgroundService>();
```

---

## 6. Visualization Feed Service (Port 5006)

### Purpose
Real-time broadcasting of simulation state to connected clients. Uses SignalR for WebSocket communication.

### Responsibilities
- SignalR hub management
- Real-time particle state broadcasting
- Client subscription management
- Universe-wide broadcasts
- Particle-specific updates

### Technology Stack
- **ASP.NET Core 10.0** - Web API framework
- **SignalR** - Real-time communication library
- **WebSocket** - Primary transport protocol

### SignalR Hub

```csharp
public class UniverseHub : Hub
{
    private readonly ILogger<UniverseHub> _logger;
    
    public UniverseHub(ILogger<UniverseHub> logger)
    {
        _logger = logger;
    }
    
    public async Task JoinUniverse(string universeId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"universe:{universeId}");
        _logger.LogInformation("Client {ConnectionId} joined universe {UniverseId}", 
            Context.ConnectionId, universeId);
    }
    
    public async Task LeaveUniverse(string universeId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"universe:{universeId}");
        _logger.LogInformation("Client {ConnectionId} left universe {UniverseId}", 
            Context.ConnectionId, universeId);
    }
    
    public async Task FollowParticle(string particleId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"particle:{particleId}");
        _logger.LogInformation("Client {ConnectionId} following particle {ParticleId}", 
            Context.ConnectionId, particleId);
    }
    
    public async Task UnfollowParticle(string particleId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"particle:{particleId}");
        _logger.LogInformation("Client {ConnectionId} unfollowed particle {ParticleId}", 
            Context.ConnectionId, particleId);
    }
    
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
```

### Broadcast Service

```csharp
public class UniverseBroadcastService
{
    private readonly IHubContext<UniverseHub> _hubContext;
    private readonly ILogger<UniverseBroadcastService> _logger;
    
    public UniverseBroadcastService(
        IHubContext<UniverseHub> hubContext, 
        ILogger<UniverseBroadcastService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }
    
    public async Task BroadcastParticleUpdateAsync(ParticleDto particle)
    {
        await _hubContext.Clients
            .Group($"universe:{particle.UniverseId}")
            .SendAsync("ParticleUpdated", particle);
        
        await _hubContext.Clients
            .Group($"particle:{particle.Id}")
            .SendAsync("ParticleUpdated", particle);
    }
    
    public async Task BroadcastUniverseStateAsync(IEnumerable<ParticleDto> particles)
    {
        await _hubContext.Clients.All.SendAsync("UniverseStateUpdated", particles);
    }
    
    public async Task BroadcastInteractionAsync(InteractionEventDto interaction)
    {
        await _hubContext.Clients.All.SendAsync("ParticleInteraction", interaction);
    }
}
```

### API Endpoint for Broadcasting

```csharp
[ApiController]
[Route("api/broadcast")]
public class BroadcastController : ControllerBase
{
    private readonly UniverseBroadcastService _broadcastService;
    
    [HttpPost("particle")]
    public async Task<IActionResult> BroadcastParticleUpdate([FromBody] ParticleDto particle)
    {
        await _broadcastService.BroadcastParticleUpdateAsync(particle);
        return Ok();
    }
    
    [HttpPost("universe")]
    public async Task<IActionResult> BroadcastUniverseState([FromBody] IEnumerable<ParticleDto> particles)
    {
        await _broadcastService.BroadcastUniverseStateAsync(particles);
        return Ok();
    }
}
```

### Client-Side Connection (JavaScript Example)

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5006/universehub")
    .configureLogging(signalR.LogLevel.Information)
    .build();

// Start connection
await connection.start();

// Join universe
await connection.invoke("JoinUniverse", "default");

// Listen for particle updates
connection.on("ParticleUpdated", (particle) => {
    console.log("Particle updated:", particle);
    // Update visualization
});

// Listen for universe state
connection.on("UniverseStateUpdated", (particles) => {
    console.log("Universe state:", particles);
    // Re-render entire universe
});

// Listen for interactions
connection.on("ParticleInteraction", (interaction) => {
    console.log("Interaction:", interaction);
    // Show visual effect
});
```

### Configuration

```csharp
// In Program.cs
builder.Services.AddSignalR();
builder.Services.AddSingleton<UniverseBroadcastService>();

var app = builder.Build();

app.MapHub<UniverseHub>("/universehub");
```

**CORS for SignalR:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000") // Frontend origin
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});
```
