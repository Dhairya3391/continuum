# Event System Documentation

## Overview
The Personal Universe Simulator uses an event-driven architecture with RabbitMQ as the message broker. Events are published by various services to communicate asynchronous operations, enabling loose coupling and scalability.

**Message Broker:** RabbitMQ  
**Client Library:** RabbitMQ.Client (v6.x)  
**Exchange Type:** Topic Exchange  
**Serialization:** JSON (System.Text.Json)

---

## Architecture

```
┌──────────────────┐     Publish      ┌─────────────────┐
│  Simulation      │─────────────────>│   RabbitMQ      │
│  Engine          │                   │   Exchange      │
└──────────────────┘                   │   (Topic)       │
                                       └─────────────────┘
┌──────────────────┐     Publish                │
│  Personality     │─────────────────>           │
│  Processing      │                             │
└──────────────────┘                             │
                                                 │ Route by
                                                 │ Routing Key
                                                 ▼
                                       ┌─────────────────┐
                                       │   Queues        │
                                       │                 │
                                       │  - particle.*   │
                                       │  - personality.*│
                                       │  - simulation.* │
                                       └─────────────────┘
                                                 │
                                                 │ Subscribe
                                                 ▼
┌──────────────────┐                   ┌─────────────────┐
│  Event Service   │◄──────────────────│  Consumers      │
│  (Logger/        │                   │                 │
│   Processor)     │                   └─────────────────┘
└──────────────────┘
        │
        │ Store Events
        ▼
┌──────────────────┐
│  Database        │
│  ParticleEvents  │
└──────────────────┘
```

---

## Exchange Configuration

**Exchange Name:** `personal-universe-exchange`  
**Exchange Type:** `topic`  
**Durability:** Durable (survives broker restart)  
**Auto-Delete:** false

**Routing Key Pattern:**
```
<entity>.<action>.<detail>
```

Examples:
- `particle.spawned`
- `particle.merged`
- `particle.expired`
- `personality.updated`
- `simulation.tick.completed`

---

## Event Definitions

### Base Event

All events inherit from `BaseEvent`:

```csharp
public abstract record BaseEvent(
    Guid EventId,        // Unique event identifier
    DateTime Timestamp   // UTC timestamp of event occurrence
);
```

---

### 1. ParticleSpawnedEvent

**Routing Key:** `particle.spawned`

**Published When:** A new particle is created for a user.

**Publisher:** Simulation Engine

**Definition:**
```csharp
public record ParticleSpawnedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid ParticleId,     // ID of the newly spawned particle
    Guid UserId,         // User who owns the particle
    double InitialX,     // Starting X position
    double InitialY      // Starting Y position
) : BaseEvent(EventId, Timestamp);
```

**Example Payload:**
```json
{
  "eventId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "timestamp": "2026-01-03T12:00:00Z",
  "particleId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "initialX": 456.78,
  "initialY": 789.23
}
```

**Consumers:**
- Event Service (logs to database)
- Visualization Feed (notifies connected clients)

**Publishing Code:**
```csharp
var evt = new ParticleSpawnedEvent(
    EventId: Guid.NewGuid(),
    Timestamp: DateTime.UtcNow,
    ParticleId: particle.Id,
    UserId: userId,
    InitialX: particle.PositionX,
    InitialY: particle.PositionY
);

await _eventPublisher.PublishAsync("particle.spawned", evt);
```

---

### 2. ParticleMergedEvent

**Routing Key:** `particle.merged`

**Published When:** Two particles merge into one due to high compatibility.

**Publisher:** Simulation Engine

**Definition:**
```csharp
public record ParticleMergedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid SourceParticleId,    // Particle that initiated merge
    Guid TargetParticleId,    // Particle that was absorbed
    Guid ResultingParticleId  // The merged particle (usually source)
) : BaseEvent(EventId, Timestamp);
```

**Example Payload:**
```json
{
  "eventId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
  "timestamp": "2026-01-03T12:05:00Z",
  "sourceParticleId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "targetParticleId": "8fa85f64-5717-4562-b3fc-2c963f66afa6",
  "resultingParticleId": "7fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Side Effects:**
- Target particle state changed to "Merged"
- Source particle mass increases
- Personality metrics recalculated for resulting particle

**Consumers:**
- Event Service (logs merge event)
- Visualization Feed (animates merge)
- Personality Processing (may trigger metric update)

---

### 3. ParticleSplitEvent

**Routing Key:** `particle.split`

**Published When:** A particle splits into multiple particles (planned feature, not yet implemented).

**Publisher:** Simulation Engine

**Definition:**
```csharp
public record ParticleSplitEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid SourceParticleId,           // Original particle
    List<Guid> ResultingParticleIds  // New particles created
) : BaseEvent(EventId, Timestamp);
```

**Example Payload:**
```json
{
  "eventId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
  "timestamp": "2026-01-03T12:10:00Z",
  "sourceParticleId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "resultingParticleIds": [
    "9fa85f64-5717-4562-b3fc-2c963f66afa6",
    "afa85f64-5717-4562-b3fc-2c963f66afa6"
  ]
}
```

**Implementation Status:** ⚠️ Event defined but split logic not yet implemented

---

### 4. ParticleExpiredEvent

**Routing Key:** `particle.expired`

**Published When:** A particle reaches 100% decay due to lack of user input.

**Publisher:** Simulation Engine (Decay Job)

**Definition:**
```csharp
public record ParticleExpiredEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid ParticleId,
    string Reason         // "Decay", "Inactivity", "Manual"
) : BaseEvent(EventId, Timestamp);
```

**Example Payload:**
```json
{
  "eventId": "d4e5f6a7-b8c9-0123-def1-234567890123",
  "timestamp": "2026-01-03T12:15:00Z",
  "particleId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "reason": "Decay"
}
```

**Triggers:**
- 24+ hours without daily input → Decay starts
- 7 days of decay → Particle expires

**Consumers:**
- Event Service (logs expiration)
- Visualization Feed (fade out animation)
- Identity Service (may send notification email)

---

### 5. ParticleInteractionEvent

**Routing Key:** `particle.interaction`

**Published When:** Two particles interact (attract, bond, or general proximity interaction).

**Publisher:** Simulation Engine

**Definition:**
```csharp
public record ParticleInteractionEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid Particle1Id,
    Guid Particle2Id,
    string InteractionType,  // "Attract", "Bond", "Proximity"
    double ImpactStrength    // Magnitude of interaction (0.0-1.0)
) : BaseEvent(EventId, Timestamp);
```

**Example Payload:**
```json
{
  "eventId": "e5f6a7b8-c9d0-1234-ef12-345678901234",
  "timestamp": "2026-01-03T12:20:00Z",
  "particle1Id": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "particle2Id": "8fa85f64-5717-4562-b3fc-2c963f66afa6",
  "interactionType": "Attract",
  "impactStrength": 0.65
}
```

**Interaction Types:**
- **Attract:** Compatible particles move toward each other
- **Bond:** Very compatible particles form temporary connection
- **Proximity:** General neighbor detection

**Consumers:**
- Event Service (logs interactions for analytics)
- Visualization Feed (shows connection lines)

---

### 6. ParticleRepelledEvent

**Routing Key:** `particle.repelled`

**Published When:** Two incompatible particles repel each other.

**Publisher:** Simulation Engine

**Definition:**
```csharp
public record ParticleRepelledEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid Particle1Id,
    Guid Particle2Id,
    double RepulsionForce    // Force applied to separate particles
) : BaseEvent(EventId, Timestamp);
```

**Example Payload:**
```json
{
  "eventId": "f6a7b8c9-d0e1-2345-f123-456789012345",
  "timestamp": "2026-01-03T12:25:00Z",
  "particle1Id": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "particle2Id": "8fa85f64-5717-4562-b3fc-2c963f66afa6",
  "repulsionForce": 12.5
}
```

**Triggers:**
- Compatibility < 0.3
- Particles within repulsion radius (typically < 30 units)

**Consumers:**
- Event Service (logs repulsions)
- Visualization Feed (shows repulsion animation)

---

### 7. PersonalityUpdatedEvent

**Routing Key:** `personality.updated`

**Published When:** User submits daily input and personality metrics are recalculated.

**Publisher:** Personality Processing Service

**Definition:**
```csharp
public record PersonalityUpdatedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid ParticleId,
    Guid UserId,
    Dictionary<string, double> UpdatedMetrics  // Trait name → value
) : BaseEvent(EventId, Timestamp);
```

**Example Payload:**
```json
{
  "eventId": "a7b8c9d0-e1f2-3456-a123-567890123456",
  "timestamp": "2026-01-03T12:30:00Z",
  "particleId": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
  "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "updatedMetrics": {
    "curiosity": 0.72,
    "socialAffinity": 0.65,
    "aggression": 0.31,
    "stability": 0.58,
    "growthPotential": 0.69
  }
}
```

**Side Effects:**
- Particle velocity may change based on new metrics
- Interaction behavior with neighbors may change

**Consumers:**
- Event Service (logs personality changes)
- Simulation Engine (updates cached metrics)
- Visualization Feed (updates particle display)

---

### 8. DailyProcessingCompletedEvent

**Routing Key:** `simulation.tick.completed`

**Published When:** Daily simulation tick completes processing.

**Publisher:** Simulation Engine (Hangfire Job)

**Definition:**
```csharp
public record DailyProcessingCompletedEvent(
    Guid EventId,
    DateTime Timestamp,
    int TickNumber,          // Sequential tick counter
    int ProcessedParticles,  // Total particles processed
    int ActiveParticles,     // Particles still active
    int ExpiredParticles     // Particles that expired this tick
) : BaseEvent(EventId, Timestamp);
```

**Example Payload:**
```json
{
  "eventId": "b8c9d0e1-f2a3-4567-b123-678901234567",
  "timestamp": "2026-01-03T12:00:00Z",
  "tickNumber": 42,
  "processedParticles": 150,
  "activeParticles": 145,
  "expiredParticles": 5
}
```

**Triggers:**
- Hangfire scheduled job (daily at midnight UTC)
- Manual tick via API endpoint

**Consumers:**
- Event Service (logs tick completion)
- Visualization Feed (broadcasts universe state)
- Monitoring/Analytics Service (tracks universe health)

---

## Event Publishing

### Publisher Service

**Interface:**
```csharp
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(string routingKey, TEvent eventData) where TEvent : BaseEvent;
}
```

**Implementation (RabbitMQ):**
```csharp
public class EventPublisher : IEventPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<EventPublisher> _logger;
    private const string ExchangeName = "personal-universe-exchange";

    public EventPublisher(IConfiguration configuration, ILogger<EventPublisher> logger)
    {
        _logger = logger;
        
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:Username"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest",
            VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/"
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Declare exchange (idempotent)
        _channel.ExchangeDeclare(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false
        );
    }

    public Task PublishAsync<TEvent>(string routingKey, TEvent eventData) where TEvent : BaseEvent
    {
        var message = JsonSerializer.Serialize(eventData);
        var body = Encoding.UTF8.GetBytes(message);

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.MessageId = eventData.EventId.ToString();
        properties.Timestamp = new AmqpTimestamp(
            ((DateTimeOffset)eventData.Timestamp).ToUnixTimeSeconds()
        );

        _channel.BasicPublish(
            exchange: ExchangeName,
            routingKey: routingKey,
            basicProperties: properties,
            body: body
        );

        _logger.LogInformation(
            "Published event {EventType} with ID {EventId} to routing key {RoutingKey}",
            typeof(TEvent).Name,
            eventData.EventId,
            routingKey
        );

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }
}
```

**Usage Example:**
```csharp
public class ParticleService
{
    private readonly IEventPublisher _eventPublisher;

    public async Task<Particle> SpawnParticleAsync(Guid userId)
    {
        // Create particle
        var particle = new Particle
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PositionX = Random.Shared.NextDouble() * 1000,
            PositionY = Random.Shared.NextDouble() * 1000,
            State = ParticleState.Active
        };

        // Save to database
        await _particleRepository.CreateAsync(particle);

        // Publish event
        var evt = new ParticleSpawnedEvent(
            EventId: Guid.NewGuid(),
            Timestamp: DateTime.UtcNow,
            ParticleId: particle.Id,
            UserId: userId,
            InitialX: particle.PositionX,
            InitialY: particle.PositionY
        );

        await _eventPublisher.PublishAsync("particle.spawned", evt);

        return particle;
    }
}
```

---

## Event Consumption

### Subscriber Service

**Interface:**
```csharp
public interface IEventSubscriber
{
    void Subscribe<TEvent>(string routingKey, Func<TEvent, Task> handler) where TEvent : BaseEvent;
}
```

**Implementation:**
```csharp
public class EventSubscriber : IEventSubscriber, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<EventSubscriber> _logger;
    private const string ExchangeName = "personal-universe-exchange";

    public EventSubscriber(IConfiguration configuration, ILogger<EventSubscriber> logger)
    {
        _logger = logger;
        
        var factory = new ConnectionFactory
        {
            HostName = configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:Username"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest",
            VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/"
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(
            exchange: ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false
        );
    }

    public void Subscribe<TEvent>(string routingKey, Func<TEvent, Task> handler) where TEvent : BaseEvent
    {
        var queueName = $"queue.{routingKey}.{typeof(TEvent).Name}";

        // Declare queue
        _channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false
        );

        // Bind queue to exchange
        _channel.QueueBind(
            queue: queueName,
            exchange: ExchangeName,
            routingKey: routingKey
        );

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                var eventData = JsonSerializer.Deserialize<TEvent>(message);
                if (eventData != null)
                {
                    await handler(eventData);
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event from {RoutingKey}", routingKey);
                _channel.BasicNack(ea.DeliveryTag, false, true); // Requeue
            }
        };

        _channel.BasicConsume(
            queue: queueName,
            autoAck: false,
            consumer: consumer
        );

        _logger.LogInformation("Subscribed to {RoutingKey} with queue {QueueName}", routingKey, queueName);
    }

    public void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }
}
```

**Usage Example (Background Service):**
```csharp
public class EventConsumerBackgroundService : BackgroundService
{
    private readonly IEventSubscriber _subscriber;
    private readonly IParticleEventRepository _eventRepository;
    private readonly ILogger<EventConsumerBackgroundService> _logger;

    public EventConsumerBackgroundService(
        IEventSubscriber subscriber,
        IParticleEventRepository eventRepository,
        ILogger<EventConsumerBackgroundService> logger)
    {
        _subscriber = subscriber;
        _eventRepository = eventRepository;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Subscribe to particle spawned events
        _subscriber.Subscribe<ParticleSpawnedEvent>("particle.spawned", async evt =>
        {
            _logger.LogInformation("Particle spawned: {ParticleId}", evt.ParticleId);

            // Log to database
            await _eventRepository.CreateAsync(new ParticleEvent
            {
                Id = Guid.NewGuid(),
                ParticleId = evt.ParticleId,
                Type = "Spawned",
                Description = $"Particle spawned at ({evt.InitialX}, {evt.InitialY})",
                OccurredAt = evt.Timestamp
            });
        });

        // Subscribe to merge events
        _subscriber.Subscribe<ParticleMergedEvent>("particle.merged", async evt =>
        {
            _logger.LogInformation(
                "Particles merged: {Source} + {Target} = {Result}",
                evt.SourceParticleId,
                evt.TargetParticleId,
                evt.ResultingParticleId
            );

            await _eventRepository.CreateAsync(new ParticleEvent
            {
                Id = Guid.NewGuid(),
                ParticleId = evt.SourceParticleId,
                TargetParticleId = evt.TargetParticleId,
                Type = "Merged",
                Description = $"Merged with particle {evt.TargetParticleId}",
                OccurredAt = evt.Timestamp
            });
        });

        // Subscribe to other events...

        return Task.CompletedTask;
    }
}
```

---

## Queue Configuration

### Queue Naming Convention
```
queue.<routing-key>.<EventTypeName>
```

Examples:
- `queue.particle.spawned.ParticleSpawnedEvent`
- `queue.particle.merged.ParticleMergedEvent`
- `queue.personality.updated.PersonalityUpdatedEvent`

### Queue Properties
- **Durable:** Yes (survives broker restart)
- **Exclusive:** No (multiple consumers allowed)
- **Auto-Delete:** No (queue persists even without consumers)
- **Message TTL:** None (messages don't expire)
- **Max Length:** 10,000 messages (then drop old ones)

---

## Error Handling

### Retry Strategy

**Failed Message Handling:**
1. Consumer throws exception
2. Message is negatively acknowledged (Nack)
3. Message requeued (redelivery=true)
4. RabbitMQ redeliver to same or different consumer
5. After 3 failed attempts, message sent to Dead Letter Queue

**Dead Letter Queue:**
- Exchange: `personal-universe-dlx`
- Queue: `dead-letter-queue`
- Manual inspection required

**Configuration:**
```csharp
var args = new Dictionary<string, object>
{
    { "x-dead-letter-exchange", "personal-universe-dlx" },
    { "x-message-ttl", 86400000 }, // 24 hours
    { "x-max-length", 10000 }
};

_channel.QueueDeclare(
    queue: queueName,
    durable: true,
    exclusive: false,
    autoDelete: false,
    arguments: args
);
```

---

## Monitoring & Observability

### RabbitMQ Management UI

**URL:** http://localhost:15672  
**Credentials:** guest/guest (default)

**Metrics to Monitor:**
- Message rate (publish/deliver)
- Queue length
- Consumer count
- Unacknowledged messages
- Message redelivery rate

### Application Logging

All publish/subscribe operations logged:
```csharp
_logger.LogInformation(
    "Published {EventType} to {RoutingKey} - EventId: {EventId}",
    typeof(TEvent).Name,
    routingKey,
    eventData.EventId
);

_logger.LogInformation(
    "Processing {EventType} from {RoutingKey} - EventId: {EventId}",
    typeof(TEvent).Name,
    routingKey,
    eventData.EventId
);
```

### Health Checks

Check RabbitMQ connection status:
```csharp
builder.Services.AddHealthChecks()
    .AddRabbitMQ(
        rabbitConnectionString: configuration.GetConnectionString("RabbitMQ"),
        name: "rabbitmq",
        failureStatus: HealthStatus.Degraded
    );
```

---

## Configuration

### appsettings.json

```json
{
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "VirtualHost": "/"
  }
}
```

### docker-compose.yml

```yaml
rabbitmq:
  image: rabbitmq:3-management
  ports:
    - "5672:5672"   # AMQP
    - "15672:15672" # Management UI
  environment:
    RABBITMQ_DEFAULT_USER: guest
    RABBITMQ_DEFAULT_PASS: guest
  volumes:
    - rabbitmq_data:/var/lib/rabbitmq
  networks:
    - personal-universe-network
```

---

## Best Practices

### Publishing
✅ **DO:**
- Always use UTC timestamps
- Include unique EventId for idempotency
- Log all published events
- Use meaningful routing keys
- Set message persistence

❌ **DON'T:**
- Publish sensitive data (passwords, tokens)
- Block on publish (already async)
- Ignore publish failures
- Use overly generic routing keys

### Consuming
✅ **DO:**
- Use background services for consumers
- Acknowledge messages after successful processing
- Implement retry logic
- Handle deserialization errors gracefully
- Log all consumed events

❌ **DON'T:**
- Auto-acknowledge before processing
- Throw exceptions without logging
- Process messages synchronously in HTTP requests
- Create circular event dependencies

---

## Future Enhancements

### Planned Features
- [ ] Event replay capability
- [ ] Event versioning for schema evolution
- [ ] Message priority queues
- [ ] Consumer scaling with load balancing
- [ ] Event store for event sourcing
- [ ] Saga pattern for complex workflows

### Schema Evolution
When events change structure:
1. Add new fields as optional
2. Keep old fields for backward compatibility
3. Version events: `ParticleSpawnedEventV2`
4. Migrate consumers gradually
