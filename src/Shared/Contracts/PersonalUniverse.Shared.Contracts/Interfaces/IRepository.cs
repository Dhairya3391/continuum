using PersonalUniverse.Shared.Models.Entities;

namespace PersonalUniverse.Shared.Contracts.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Guid> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
}

public interface IParticleRepository : IRepository<Particle>
{
    Task<Particle?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Particle>> GetActiveParticlesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Particle>> GetParticlesInRegionAsync(double minX, double maxX, double minY, double maxY, CancellationToken cancellationToken = default);
}

public interface IPersonalityMetricsRepository : IRepository<PersonalityMetrics>
{
    Task<PersonalityMetrics?> GetLatestByParticleIdAsync(Guid particleId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PersonalityMetrics>> GetHistoryByParticleIdAsync(Guid particleId, CancellationToken cancellationToken = default);
}

public interface IParticleEventRepository : IRepository<ParticleEvent>
{
    Task<IEnumerable<ParticleEvent>> GetEventsByParticleIdAsync(Guid particleId, DateTime? since = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<ParticleEvent>> GetEventsByTypeAsync(EventType eventType, DateTime? since = null, CancellationToken cancellationToken = default);
}

public interface IDailyInputRepository : IRepository<DailyInput>
{
    Task<IEnumerable<DailyInput>> GetUnprocessedInputsAsync(CancellationToken cancellationToken = default);
    Task<int> GetDailyInputCountAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default);
}

public interface IUniverseStateRepository : IRepository<UniverseState>
{
    Task<UniverseState?> GetLatestStateAsync(CancellationToken cancellationToken = default);
    Task<UniverseState?> GetStateByTickAsync(int tickNumber, CancellationToken cancellationToken = default);
}
