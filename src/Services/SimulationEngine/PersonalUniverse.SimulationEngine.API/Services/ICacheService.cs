using PersonalUniverse.Shared.Models.Entities;

namespace PersonalUniverse.SimulationEngine.API.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task<IEnumerable<Particle>> GetActiveParticlesAsync(CancellationToken cancellationToken = default);
    Task SetActiveParticlesAsync(IEnumerable<Particle> particles, CancellationToken cancellationToken = default);
    Task CacheParticleAsync(Particle particle, CancellationToken cancellationToken = default);
    Task<Particle?> GetParticleAsync(Guid particleId, CancellationToken cancellationToken = default);
    Task InvalidateParticleAsync(Guid particleId, CancellationToken cancellationToken = default);
    Task CachePersonalityMetricsAsync(PersonalityMetrics metrics, CancellationToken cancellationToken = default);
    Task<PersonalityMetrics?> GetPersonalityMetricsAsync(Guid particleId, CancellationToken cancellationToken = default);
}
