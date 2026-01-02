using PersonalUniverse.Shared.Contracts.Interfaces;
using PersonalUniverse.Shared.Models.Entities;
using PersonalUniverse.Shared.Models.DTOs;

namespace PersonalUniverse.SimulationEngine.API.Services;

public interface ISimulationService
{
    Task<UniverseStateDto> GetUniverseStateAsync(CancellationToken cancellationToken = default);
    Task ProcessSimulationTickAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<ParticleDto>> FindNeighborsAsync(Guid particleId, double radius, CancellationToken cancellationToken = default);
}

public class SimulationService : ISimulationService
{
    private readonly IParticleRepository _particleRepository;
    private readonly IPersonalityMetricsRepository _metricsRepository;
    private readonly IUniverseStateRepository _stateRepository;
    private readonly ILogger<SimulationService> _logger;
    private const double InteractionRadius = 50.0;

    public SimulationService(
        IParticleRepository particleRepository,
        IPersonalityMetricsRepository metricsRepository,
        IUniverseStateRepository stateRepository,
        ILogger<SimulationService> logger)
    {
        _particleRepository = particleRepository;
        _metricsRepository = metricsRepository;
        _stateRepository = stateRepository;
        _logger = logger;
    }

    public async Task<UniverseStateDto> GetUniverseStateAsync(CancellationToken cancellationToken = default)
    {
        var particles = await _particleRepository.GetActiveParticlesAsync(cancellationToken);
        var particleList = particles.ToList();

        var latestState = await _stateRepository.GetLatestStateAsync(cancellationToken);
        var tickNumber = latestState?.TickNumber ?? 0;

        var averageEnergy = particleList.Any() ? particleList.Average(p => p.Energy) : 0;

        var particleDtos = particleList.Select(p => new ParticleDto(
            p.Id,
            p.UserId,
            p.PositionX,
            p.PositionY,
            p.VelocityX,
            p.VelocityY,
            p.Mass,
            p.Energy,
            p.State.ToString(),
            p.DecayLevel
        )).ToList();

        return new UniverseStateDto(
            tickNumber,
            DateTime.UtcNow,
            particleList.Count,
            averageEnergy,
            0, // Interaction count will be tracked separately
            particleDtos
        );
    }

    public async Task ProcessSimulationTickAsync(CancellationToken cancellationToken = default)
    {
        var particles = await _particleRepository.GetActiveParticlesAsync(cancellationToken);
        var particleList = particles.ToList();

        var latestState = await _stateRepository.GetLatestStateAsync(cancellationToken);
        var nextTick = (latestState?.TickNumber ?? 0) + 1;

        int interactionCount = 0;

        // Process each particle
        foreach (var particle in particleList)
        {
            // Apply velocity to position
            particle.PositionX += particle.VelocityX;
            particle.PositionY += particle.VelocityY;

            // Wrap around universe boundaries
            particle.PositionX = ((particle.PositionX % 1000) + 1000) % 1000;
            particle.PositionY = ((particle.PositionY % 1000) + 1000) % 1000;

            // Check for decay (if no input in last 24 hours)
            if (particle.LastInputAt.HasValue)
            {
                var hoursSinceInput = (DateTime.UtcNow - particle.LastInputAt.Value).TotalHours;
                if (hoursSinceInput > 24)
                {
                    particle.DecayLevel++;
                    particle.Energy = Math.Max(0, particle.Energy - 5);
                    
                    if (particle.DecayLevel >= 3 || particle.Energy <= 0)
                    {
                        particle.State = ParticleState.Expired;
                    }
                    else
                    {
                        particle.State = ParticleState.Decaying;
                    }
                }
            }

            // Update particle
            await _particleRepository.UpdateAsync(particle, cancellationToken);

            // Find and process interactions
            var neighbors = await FindNearbyParticles(particle, InteractionRadius, cancellationToken);
            interactionCount += neighbors.Count();
        }

        // Save universe state snapshot
        var averageEnergy = particleList.Any() ? particleList.Average(p => p.Energy) : 0;
        var universeState = new UniverseState
        {
            TickNumber = nextTick,
            Timestamp = DateTime.UtcNow,
            ActiveParticleCount = particleList.Count(p => p.State == ParticleState.Active),
            AverageEnergy = averageEnergy,
            InteractionCount = interactionCount,
            SnapshotData = "{}" // Could serialize full state here
        };

        await _stateRepository.AddAsync(universeState, cancellationToken);

        _logger.LogInformation("Processed simulation tick {Tick} with {Count} particles and {Interactions} interactions",
            nextTick, particleList.Count, interactionCount);
    }

    public async Task<IEnumerable<ParticleDto>> FindNeighborsAsync(Guid particleId, double radius, CancellationToken cancellationToken = default)
    {
        var particle = await _particleRepository.GetByIdAsync(particleId, cancellationToken);
        if (particle == null)
        {
            return Enumerable.Empty<ParticleDto>();
        }

        var neighbors = await FindNearbyParticles(particle, radius, cancellationToken);
        return neighbors.Select(p => new ParticleDto(
            p.Id,
            p.UserId,
            p.PositionX,
            p.PositionY,
            p.VelocityX,
            p.VelocityY,
            p.Mass,
            p.Energy,
            p.State.ToString(),
            p.DecayLevel
        ));
    }

    private async Task<IEnumerable<Particle>> FindNearbyParticles(Particle particle, double radius, CancellationToken cancellationToken)
    {
        var minX = particle.PositionX - radius;
        var maxX = particle.PositionX + radius;
        var minY = particle.PositionY - radius;
        var maxY = particle.PositionY + radius;

        var candidates = await _particleRepository.GetParticlesInRegionAsync(minX, maxX, minY, maxY, cancellationToken);
        
        return candidates.Where(p => 
            p.Id != particle.Id && 
            CalculateDistance(particle, p) <= radius);
    }

    private static double CalculateDistance(Particle p1, Particle p2)
    {
        var dx = p1.PositionX - p2.PositionX;
        var dy = p1.PositionY - p2.PositionY;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
