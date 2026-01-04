using PersonalUniverse.Shared.Models.Entities;
using StackExchange.Redis;
using System.Text.Json;

namespace PersonalUniverse.SimulationEngine.API.Services;

public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly TimeSpan _defaultExpiry = TimeSpan.FromMinutes(30);
    private const string ActiveParticlesKey = "simulation:active_particles";
    private const string UniverseStateKey = "simulation:universe_state";
    private const string ParticlePrefix = "particle:";
    private const string PersonalityPrefix = "personality:";

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);
            
            if (value.IsNullOrEmpty)
            {
                _logger.LogDebug("Cache miss for key {Key}", key);
                return default;
            }

            _logger.LogDebug("Cache hit for key {Key}", key);
            return JsonSerializer.Deserialize<T>((string)value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cached value for key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var serialized = JsonSerializer.Serialize(value);
            await db.StringSetAsync(key, serialized, expiry ?? _defaultExpiry);
            _logger.LogDebug("Cached value for key {Key} with expiry {Expiry}", key, expiry ?? _defaultExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cached value for key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
            _logger.LogDebug("Removed cached value for key {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cached value for key {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            return await db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if key exists {Key}", key);
            return false;
        }
    }

    public async Task<IEnumerable<Particle>> GetActiveParticlesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(ActiveParticlesKey);
            
            if (value.IsNullOrEmpty)
            {
                _logger.LogDebug("Active particles cache miss");
                return Enumerable.Empty<Particle>();
            }

            _logger.LogDebug("Active particles cache hit");
            return JsonSerializer.Deserialize<IEnumerable<Particle>>((string)value!) ?? Enumerable.Empty<Particle>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active particles from cache");
            return Enumerable.Empty<Particle>();
        }
    }

    public async Task SetActiveParticlesAsync(IEnumerable<Particle> particles, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var serialized = JsonSerializer.Serialize(particles);
            await db.StringSetAsync(ActiveParticlesKey, serialized, TimeSpan.FromMinutes(5));
            _logger.LogDebug("Cached {Count} active particles", particles.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting active particles in cache");
        }
    }

    public async Task CacheParticleAsync(Particle particle, CancellationToken cancellationToken = default)
    {
        await SetAsync($"{ParticlePrefix}{particle.Id}", particle, TimeSpan.FromMinutes(15), cancellationToken);
    }

    public async Task<Particle?> GetParticleAsync(Guid particleId, CancellationToken cancellationToken = default)
    {
        return await GetAsync<Particle>($"{ParticlePrefix}{particleId}", cancellationToken);
    }

    public async Task InvalidateParticleAsync(Guid particleId, CancellationToken cancellationToken = default)
    {
        await RemoveAsync($"{ParticlePrefix}{particleId}", cancellationToken);
    }

    public async Task CachePersonalityMetricsAsync(PersonalityMetrics metrics, CancellationToken cancellationToken = default)
    {
        await SetAsync($"{PersonalityPrefix}{metrics.ParticleId}", metrics, TimeSpan.FromHours(1), cancellationToken);
    }

    public async Task<PersonalityMetrics?> GetPersonalityMetricsAsync(Guid particleId, CancellationToken cancellationToken = default)
    {
        return await GetAsync<PersonalityMetrics>($"{PersonalityPrefix}{particleId}", cancellationToken);
    }

    public async Task CacheUniverseStateAsync(UniverseState state, CancellationToken cancellationToken = default)
    {
        await SetAsync(UniverseStateKey, state, TimeSpan.FromMinutes(10), cancellationToken);
    }

    public async Task<UniverseState?> GetUniverseStateAsync(CancellationToken cancellationToken = default)
    {
        return await GetAsync<UniverseState>(UniverseStateKey, cancellationToken);
    }

    public async Task ClearSimulationCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var server = _redis.GetServer(_redis.GetEndPoints().First());
            
            var keys = server.Keys(pattern: "simulation:*").ToArray();
            if (keys.Length > 0)
            {
                await db.KeyDeleteAsync(keys);
                _logger.LogInformation("Cleared {Count} simulation cache keys", keys.Length);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing simulation cache");
        }
    }
}
