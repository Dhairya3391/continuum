# Caching Strategy Documentation

## Overview
The Personal Universe Simulator uses Redis as a distributed cache to optimize performance and reduce database load. Caching is primarily used in the Simulation Engine for frequently accessed particle data.

**Cache Technology:** Redis (StackExchange.Redis)  
**Cache Location:** Centralized Redis server  
**TTL Strategy:** Time-based expiration with sliding windows  
**Invalidation:** Manual invalidation on updates

---

## Architecture

```
┌──────────────────────┐
│  Simulation Engine   │
│                      │
│  ┌────────────────┐  │
│  │ Cache Service  │  │
│  │ (Redis Client) │  │
│  └───────┬────────┘  │
└──────────┼───────────┘
           │
           │ GET/SET/DELETE
           ▼
    ┌──────────────┐
    │    Redis     │
    │    Server    │
    │  (Port 6379) │
    └──────┬───────┘
           │
           │ If Cache Miss
           ▼
    ┌──────────────┐
    │  SQL Server  │
    │   Database   │
    └──────────────┘
```

---

## Cache Patterns

### 1. Cache-Aside (Lazy Loading)

**Most common pattern** - Application checks cache first, loads from database on miss.

**Flow:**
```
1. Check Redis for data
2. If found → Return cached data
3. If not found:
   a. Query database
   b. Store in Redis with TTL
   c. Return data
```

**Implementation:**
```csharp
public async Task<Particle?> GetParticleByIdAsync(Guid particleId, CancellationToken cancellationToken)
{
    var cacheKey = $"particle:{particleId}";

    // 1. Try to get from cache
    var cachedParticle = await _cacheService.GetAsync<Particle>(cacheKey, cancellationToken);
    if (cachedParticle != null)
    {
        _logger.LogDebug("Cache hit for particle {ParticleId}", particleId);
        return cachedParticle;
    }

    // 2. Cache miss - get from database
    _logger.LogDebug("Cache miss for particle {ParticleId}, fetching from database", particleId);
    var particle = await _particleRepository.GetByIdAsync(particleId, cancellationToken);

    if (particle != null)
    {
        // 3. Store in cache with 10-minute TTL
        await _cacheService.SetAsync(
            cacheKey, 
            particle, 
            TimeSpan.FromMinutes(10), 
            cancellationToken
        );
    }

    return particle;
}
```

---

### 2. Write-Through

**Data written to cache and database simultaneously** - Ensures consistency but slower writes.

**Flow:**
```
1. Write to database
2. Write to cache (or invalidate)
3. Return success
```

**Implementation:**
```csharp
public async Task UpdateParticleAsync(Particle particle, CancellationToken cancellationToken)
{
    // 1. Update database
    await _particleRepository.UpdateAsync(particle, cancellationToken);

    // 2. Update cache
    var cacheKey = $"particle:{particle.Id}";
    await _cacheService.SetAsync(
        cacheKey, 
        particle, 
        TimeSpan.FromMinutes(10), 
        cancellationToken
    );

    _logger.LogInformation("Updated particle {ParticleId} in database and cache", particle.Id);
}
```

---

### 3. Cache Invalidation

**Remove stale data** - Used when data changes and cache needs to be cleared.

**Implementation:**
```csharp
public async Task DeleteParticleAsync(Guid particleId, CancellationToken cancellationToken)
{
    // 1. Delete from database
    await _particleRepository.DeleteAsync(particleId, cancellationToken);

    // 2. Invalidate cache
    var cacheKey = $"particle:{particleId}";
    await _cacheService.DeleteAsync(cacheKey, cancellationToken);

    _logger.LogInformation("Deleted particle {ParticleId} from database and cache", particleId);
}
```

---

## Redis Cache Service

### Interface Definition

```csharp
public interface IRedisCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteByPatternAsync(string pattern, CancellationToken cancellationToken = default);
}
```

### Implementation

```csharp
public class RedisCacheService : IRedisCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(
        IConnectionMultiplexer redis, 
        ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _database = redis.GetDatabase();
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _database.StringGetAsync(key);
            
            if (value.IsNullOrEmpty)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving from cache: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key, 
        T value, 
        TimeSpan? expiry = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var serialized = JsonSerializer.Serialize(value);
            await _database.StringSetAsync(key, serialized, expiry);
            
            _logger.LogDebug("Cached {Key} with expiry {Expiry}", key, expiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _database.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache existence: {Key}", key);
            return false;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _database.KeyDeleteAsync(key);
            _logger.LogDebug("Deleted cache key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting cache: {Key}", key);
        }
    }

    public async Task DeleteByPatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            var keys = server.Keys(pattern: pattern).ToArray();
            
            if (keys.Length > 0)
            {
                await _database.KeyDeleteAsync(keys);
                _logger.LogDebug("Deleted {Count} keys matching pattern: {Pattern}", keys.Length, pattern);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting by pattern: {Pattern}", pattern);
        }
    }
}
```

---

## Cached Data Types

### 1. Particle Data

**Cache Key:** `particle:{particleId}`  
**TTL:** 10 minutes  
**Reason:** Frequently accessed during simulation ticks

```csharp
await _cacheService.SetAsync(
    $"particle:{particleId}", 
    particle, 
    TimeSpan.FromMinutes(10)
);
```

**Invalidation:**
- On particle update
- On particle state change (Active → Decaying → Expired)
- On particle merge/split

---

### 2. Active Particles List

**Cache Key:** `particles:active`  
**TTL:** 5 minutes  
**Reason:** Frequently queried for universe state

```csharp
var activeParticles = await _particleRepository.GetActiveParticlesAsync();
await _cacheService.SetAsync(
    "particles:active", 
    activeParticles, 
    TimeSpan.FromMinutes(5)
);
```

**Invalidation:**
- After simulation tick
- When any particle spawns/expires

---

### 3. User Particle Mapping

**Cache Key:** `particle:user:{userId}`  
**TTL:** 15 minutes  
**Reason:** Quick lookup of user's current particle

```csharp
await _cacheService.SetAsync(
    $"particle:user:{userId}", 
    particle.Id, 
    TimeSpan.FromMinutes(15)
);
```

**Invalidation:**
- When particle expires
- When new particle spawns

---

### 4. Personality Metrics

**Cache Key:** `personality:{particleId}`  
**TTL:** 30 minutes  
**Reason:** Metrics change infrequently (only with daily input)

```csharp
await _cacheService.SetAsync(
    $"personality:{particleId}", 
    metrics, 
    TimeSpan.FromMinutes(30)
);
```

**Invalidation:**
- After daily input processing
- After particle merge (metrics recalculated)

---

### 5. Universe State

**Cache Key:** `universe:state`  
**TTL:** 2 minutes  
**Reason:** Full universe snapshot for visualization

```csharp
var universeState = new UniverseStateDto
{
    TickNumber = currentTick,
    Particles = activeParticles,
    Timestamp = DateTime.UtcNow
};

await _cacheService.SetAsync(
    "universe:state", 
    universeState, 
    TimeSpan.FromMinutes(2)
);
```

**Invalidation:**
- After simulation tick
- Manual refresh via API

---

## TTL Strategy

### TTL Guidelines

| Data Type | TTL | Rationale |
|-----------|-----|-----------|
| Particle (individual) | 10 min | Balance freshness vs. query reduction |
| Active particles list | 5 min | Changes frequently during tick |
| User-particle mapping | 15 min | Rarely changes (only on spawn/expire) |
| Personality metrics | 30 min | Changes max 3x/day per user |
| Universe state | 2 min | Must be relatively real-time |
| Neighbor calculations | 1 min | Physics-dependent, changes rapidly |

### Sliding Expiration

**Extend TTL on access** - Keep frequently accessed data cached longer.

```csharp
public async Task<T?> GetWithSlidingExpirationAsync<T>(
    string key, 
    TimeSpan slidingWindow, 
    CancellationToken cancellationToken = default)
{
    var value = await GetAsync<T>(key, cancellationToken);
    
    if (value != null)
    {
        // Reset expiration on access
        await _database.KeyExpireAsync(key, slidingWindow);
    }
    
    return value;
}
```

---

## Cache Configuration

### Registration (Program.cs)

```csharp
// Redis connection
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") 
    ?? "localhost:6379";

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(redisConnectionString);
    configuration.AbortOnConnectFail = false; // Don't crash if Redis unavailable
    configuration.ConnectTimeout = 5000;
    configuration.SyncTimeout = 5000;
    return ConnectionMultiplexer.Connect(configuration);
});

// Register cache service
builder.Services.AddSingleton<IRedisCacheService, RedisCacheService>();
```

### appsettings.json

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,password=your-redis-password,ssl=false,abortConnect=false"
  },
  "Redis": {
    "DefaultExpiry": "00:10:00",
    "EnableLogging": true
  }
}
```

### docker-compose.yml

```yaml
redis:
  image: redis:7-alpine
  ports:
    - "6379:6379"
  command: redis-server --appendonly yes
  volumes:
    - redis_data:/data
  networks:
    - personal-universe-network
  healthcheck:
    test: ["CMD", "redis-cli", "ping"]
    interval: 10s
    timeout: 3s
    retries: 5
```

---

## Cache Key Naming Convention

### Pattern

```
{entity}:{identifier}:{optional-qualifier}
```

### Examples

```
particle:7fa85f64-5717-4562-b3fc-2c963f66afa6
particle:user:3fa85f64-5717-4562-b3fc-2c963f66afa6
personality:7fa85f64-5717-4562-b3fc-2c963f66afa6
particles:active
particles:neighbors:7fa85f64-5717-4562-b3fc-2c963f66afa6
universe:state
universe:tick:42
```

### Benefits

- **Organized:** Easy to understand structure
- **Searchable:** Pattern matching (e.g., `particle:*`)
- **Collision-free:** Namespaced by entity type

---

## Cache Invalidation Strategies

### 1. Time-Based (TTL)

**Automatic expiration** - Redis removes key after TTL expires.

```csharp
await _cacheService.SetAsync(key, value, TimeSpan.FromMinutes(10));
```

---

### 2. Event-Based

**Invalidate on events** - Remove cache when data changes.

```csharp
// When particle merges
public async Task MergeParticlesAsync(Guid sourceId, Guid targetId)
{
    // Perform merge
    await _particleRepository.MergeAsync(sourceId, targetId);

    // Invalidate both particles
    await _cacheService.DeleteAsync($"particle:{sourceId}");
    await _cacheService.DeleteAsync($"particle:{targetId}");

    // Invalidate active particles list
    await _cacheService.DeleteAsync("particles:active");
}
```

---

### 3. Manual Invalidation

**API endpoint for cache clearing** - Admin tool for debugging.

```csharp
[HttpDelete("cache/{pattern}")]
[Authorize(Roles = "Admin")]
public async Task<IActionResult> ClearCache(string pattern)
{
    await _cacheService.DeleteByPatternAsync(pattern);
    return Ok(new { message = $"Cleared cache matching: {pattern}" });
}
```

---

### 4. Batch Invalidation

**Clear related keys together** - More efficient than individual deletes.

```csharp
public async Task InvalidateParticleDataAsync(Guid particleId)
{
    var keysToDelete = new[]
    {
        $"particle:{particleId}",
        $"personality:{particleId}",
        $"particles:neighbors:{particleId}"
    };

    foreach (var key in keysToDelete)
    {
        await _cacheService.DeleteAsync(key);
    }
}
```

---

## Performance Optimization

### Batch Operations

**Get multiple keys at once** - Reduce round trips to Redis.

```csharp
public async Task<Dictionary<Guid, Particle>> GetParticlesBatchAsync(
    IEnumerable<Guid> particleIds, 
    CancellationToken cancellationToken)
{
    var keys = particleIds.Select(id => (RedisKey)$"particle:{id}").ToArray();
    var values = await _database.StringGetAsync(keys);

    var result = new Dictionary<Guid, Particle>();
    for (int i = 0; i < keys.Length; i++)
    {
        if (!values[i].IsNullOrEmpty)
        {
            var particle = JsonSerializer.Deserialize<Particle>(values[i]!);
            if (particle != null)
            {
                result[particleIds.ElementAt(i)] = particle;
            }
        }
    }

    return result;
}
```

---

### Pipeline Commands

**Queue multiple operations** - Execute in single round trip.

```csharp
public async Task UpdateMultipleParticlesAsync(IEnumerable<Particle> particles)
{
    var batch = _database.CreateBatch();
    var tasks = new List<Task>();

    foreach (var particle in particles)
    {
        var key = $"particle:{particle.Id}";
        var serialized = JsonSerializer.Serialize(particle);
        tasks.Add(batch.StringSetAsync(key, serialized, TimeSpan.FromMinutes(10)));
    }

    batch.Execute();
    await Task.WhenAll(tasks);
}
```

---

### Compression

**Compress large cached objects** - Reduce memory usage.

```csharp
public async Task SetCompressedAsync<T>(string key, T value, TimeSpan? expiry = null)
{
    var serialized = JsonSerializer.Serialize(value);
    var bytes = Encoding.UTF8.GetBytes(serialized);

    using var outputStream = new MemoryStream();
    using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
    {
        await gzipStream.WriteAsync(bytes, 0, bytes.Length);
    }

    var compressed = outputStream.ToArray();
    await _database.StringSetAsync(key, compressed, expiry);
}
```

---

## Monitoring & Observability

### Redis Metrics

**Monitor via Redis CLI:**
```bash
redis-cli INFO stats
redis-cli INFO memory
redis-cli MONITOR  # Real-time command logging
```

**Key Metrics:**
- Hit rate: `keyspace_hits / (keyspace_hits + keyspace_misses)`
- Memory usage: `used_memory_human`
- Evicted keys: `evicted_keys`
- Connected clients: `connected_clients`

### Application Logging

```csharp
_logger.LogInformation(
    "Cache operation: {Operation} for key {Key} - Result: {Result}",
    "GET",
    cacheKey,
    cachedData != null ? "HIT" : "MISS"
);
```

### Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddRedis(
        redisConnectionString: builder.Configuration.GetConnectionString("Redis")!,
        name: "redis",
        failureStatus: HealthStatus.Degraded
    );
```

---

## Fallback Strategy

### Graceful Degradation

**If Redis unavailable, fall back to database** - Don't crash the application.

```csharp
public async Task<Particle?> GetParticleByIdAsync(Guid particleId)
{
    try
    {
        // Try cache first
        var cached = await _cacheService.GetAsync<Particle>($"particle:{particleId}");
        if (cached != null)
        {
            return cached;
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Redis unavailable, falling back to database");
    }

    // Fall back to database
    return await _particleRepository.GetByIdAsync(particleId);
}
```

---

## Best Practices

### Do's ✅

- Use descriptive cache keys with namespaces
- Set appropriate TTLs based on data volatility
- Implement cache invalidation on writes
- Log cache hits/misses for monitoring
- Handle Redis failures gracefully
- Use batch operations for multiple keys
- Compress large objects before caching

### Don'ts ❌

- Don't cache everything (balance cost vs. benefit)
- Don't set infinite TTLs (leads to stale data)
- Don't ignore cache eviction policies
- Don't cache sensitive data without encryption
- Don't use cache as primary data store
- Don't forget to invalidate on updates
- Don't make cache failures catastrophic

---

## Future Enhancements

- [ ] Redis Cluster for horizontal scaling
- [ ] Cache warming on application startup
- [ ] Distributed locking for cache updates
- [ ] Cache statistics dashboard
- [ ] Automatic cache key versioning
- [ ] Redis Streams for event caching
- [ ] Multi-level caching (L1: memory, L2: Redis)
