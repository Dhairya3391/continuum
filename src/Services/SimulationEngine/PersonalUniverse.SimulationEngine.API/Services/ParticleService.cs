using PersonalUniverse.Shared.Contracts.Interfaces;
using PersonalUniverse.Shared.Models.Entities;
using PersonalUniverse.Shared.Models.DTOs;

namespace PersonalUniverse.SimulationEngine.API.Services;

public interface IParticleService
{
    Task<ParticleDto> SpawnParticleAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ParticleDto>> GetActiveParticlesAsync(CancellationToken cancellationToken = default);
    Task<ParticleDto?> GetParticleByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> UpdateParticleStateAsync(ParticleUpdateDto updateDto, CancellationToken cancellationToken = default);
}

public class ParticleService : IParticleService
{
    private readonly IParticleRepository _particleRepository;
    private readonly IPersonalityMetricsRepository _metricsRepository;
    private readonly ILogger<ParticleService> _logger;
    private const double UniverseSize = 1000.0;

    public ParticleService(
        IParticleRepository particleRepository,
        IPersonalityMetricsRepository metricsRepository,
        ILogger<ParticleService> logger)
    {
        _particleRepository = particleRepository;
        _metricsRepository = metricsRepository;
        _logger = logger;
    }

    public async Task<ParticleDto> SpawnParticleAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Check if user already has a particle
        var existing = await _particleRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existing != null)
        {
            _logger.LogWarning("User {UserId} already has a particle {ParticleId}", userId, existing.Id);
            return MapToDto(existing);
        }

        // Create new particle at random position
        var particle = new Particle
        {
            UserId = userId,
            PositionX = Random.Shared.NextDouble() * UniverseSize,
            PositionY = Random.Shared.NextDouble() * UniverseSize,
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

        var particleId = await _particleRepository.AddAsync(particle, cancellationToken);
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

        await _metricsRepository.AddAsync(metrics, cancellationToken);

        _logger.LogInformation("Spawned particle {ParticleId} for user {UserId} at ({X}, {Y})", 
            particleId, userId, particle.PositionX, particle.PositionY);

        return MapToDto(particle);
    }

    public async Task<IEnumerable<ParticleDto>> GetActiveParticlesAsync(CancellationToken cancellationToken = default)
    {
        var particles = await _particleRepository.GetActiveParticlesAsync(cancellationToken);
        return particles.Select(MapToDto);
    }

    public async Task<ParticleDto?> GetParticleByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var particle = await _particleRepository.GetByUserIdAsync(userId, cancellationToken);
        return particle == null ? null : MapToDto(particle);
    }

    public async Task<bool> UpdateParticleStateAsync(ParticleUpdateDto updateDto, CancellationToken cancellationToken = default)
    {
        var particle = await _particleRepository.GetByIdAsync(updateDto.ParticleId, cancellationToken);
        if (particle == null)
        {
            return false;
        }

        particle.PositionX = updateDto.PositionX;
        particle.PositionY = updateDto.PositionY;
        particle.VelocityX = updateDto.VelocityX;
        particle.VelocityY = updateDto.VelocityY;
        particle.Energy = updateDto.Energy;
        particle.State = Enum.Parse<ParticleState>(updateDto.State);
        particle.LastUpdatedAt = DateTime.UtcNow;

        return await _particleRepository.UpdateAsync(particle, cancellationToken);
    }

    private static ParticleDto MapToDto(Particle particle)
    {
        return new ParticleDto(
            particle.Id,
            particle.UserId,
            particle.PositionX,
            particle.PositionY,
            particle.VelocityX,
            particle.VelocityY,
            particle.Mass,
            particle.Energy,
            particle.State.ToString(),
            particle.DecayLevel
        );
    }
}
