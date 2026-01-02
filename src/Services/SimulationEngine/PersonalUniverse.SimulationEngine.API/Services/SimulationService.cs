using PersonalUniverse.Shared.Contracts.Interfaces;
using PersonalUniverse.Shared.Models.Entities;
using PersonalUniverse.Shared.Models.DTOs;
using PersonalUniverse.Shared.Contracts.Events;

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
    private readonly IInteractionService _interactionService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SimulationService> _logger;
    private const double InteractionRadius = 50.0;

    public SimulationService(
        IParticleRepository particleRepository,
        IPersonalityMetricsRepository metricsRepository,
        IUniverseStateRepository stateRepository,
        IInteractionService interactionService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<SimulationService> logger)
    {
        _particleRepository = particleRepository;
        _metricsRepository = metricsRepository;
        _stateRepository = stateRepository;
        _interactionService = interactionService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
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
            if (particle.State == ParticleState.Active)
            {
                var neighbors = await FindNearbyParticles(particle, InteractionRadius, cancellationToken);
                foreach (var neighbor in neighbors.Where(n => n.State == ParticleState.Active))
                {
                    await ProcessInteractionAsync(particle, neighbor, cancellationToken);
                    interactionCount++;
                }
            }
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

    private async Task ProcessInteractionAsync(Particle p1, Particle p2, CancellationToken cancellationToken)
    {
        try
        {
            var interactionResult = await _interactionService.EvaluateInteractionAsync(p1.Id, p2.Id, cancellationToken);
            
            switch (interactionResult.Type)
            {
                case InteractionType.Merge:
                    await HandleMergeAsync(p1, p2, interactionResult.Strength, cancellationToken);
                    break;
                    
                case InteractionType.Bond:
                    await HandleBondAsync(p1, p2, interactionResult.Strength, cancellationToken);
                    break;
                    
                case InteractionType.Repel:
                    await HandleRepelAsync(p1, p2, interactionResult.Strength, cancellationToken);
                    break;
                    
                case InteractionType.Attract:
                    await HandleAttractAsync(p1, p2, interactionResult.Strength, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing interaction between {P1} and {P2}", p1.Id, p2.Id);
        }
    }

    private async Task HandleMergeAsync(Particle p1, Particle p2, double strength, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Merging particles {P1} and {P2} with strength {Strength}", p1.Id, p2.Id, strength);
        
        // Merge into p1, expire p2
        p1.Mass += p2.Mass;
        p1.Energy = Math.Min(100, p1.Energy + (p2.Energy * 0.5));
        p2.State = ParticleState.Expired;
        
        await _particleRepository.UpdateAsync(p1, cancellationToken);
        await _particleRepository.UpdateAsync(p2, cancellationToken);
        
        // Publish event
        await PublishEventAsync(new ParticleMergedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            p2.Id,
            p1.Id,
            p1.Id
        ));
    }

    private async Task HandleBondAsync(Particle p1, Particle p2, double strength, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Bonding particles {P1} and {P2} with strength {Strength}", p1.Id, p2.Id, strength);
        
        // Align velocities (move together)
        var avgVelX = (p1.VelocityX + p2.VelocityX) / 2;
        var avgVelY = (p1.VelocityY + p2.VelocityY) / 2;
        
        p1.VelocityX = avgVelX * 0.7 + p1.VelocityX * 0.3;
        p1.VelocityY = avgVelY * 0.7 + p1.VelocityY * 0.3;
        p2.VelocityX = avgVelX * 0.7 + p2.VelocityX * 0.3;
        p2.VelocityY = avgVelY * 0.7 + p2.VelocityY * 0.3;
        
        // Small energy exchange
        p1.Energy = Math.Min(100, p1.Energy + 1);
        p2.Energy = Math.Min(100, p2.Energy + 1);
        
        await _particleRepository.UpdateAsync(p1, cancellationToken);
        await _particleRepository.UpdateAsync(p2, cancellationToken);
        
        // Publish event
        await PublishEventAsync(new ParticleInteractionEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            p1.Id,
            p2.Id,
            "Bond",
            strength
        ));
    }

    private async Task HandleRepelAsync(Particle p1, Particle p2, double strength, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Repelling particles {P1} and {P2} with strength {Strength}", p1.Id, p2.Id, strength);
        
        // Calculate repulsion vector
        var dx = p1.PositionX - p2.PositionX;
        var dy = p1.PositionY - p2.PositionY;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        
        if (distance > 0)
        {
            var force = strength * 0.5;
            p1.VelocityX += (dx / distance) * force;
            p1.VelocityY += (dy / distance) * force;
            p2.VelocityX -= (dx / distance) * force;
            p2.VelocityY -= (dy / distance) * force;
            
            // Clamp velocities
            p1.VelocityX = Math.Clamp(p1.VelocityX, -5, 5);
            p1.VelocityY = Math.Clamp(p1.VelocityY, -5, 5);
            p2.VelocityX = Math.Clamp(p2.VelocityX, -5, 5);
            p2.VelocityY = Math.Clamp(p2.VelocityY, -5, 5);
            
            await _particleRepository.UpdateAsync(p1, cancellationToken);
            await _particleRepository.UpdateAsync(p2, cancellationToken);
        }
        
        // Publish event
        await PublishEventAsync(new ParticleRepelledEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            p1.Id,
            p2.Id,
            strength
        ));
    }

    private async Task HandleAttractAsync(Particle p1, Particle p2, double strength, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Attracting particles {P1} and {P2} with strength {Strength}", p1.Id, p2.Id, strength);
        
        // Calculate attraction vector
        var dx = p2.PositionX - p1.PositionX;
        var dy = p2.PositionY - p1.PositionY;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        
        if (distance > 0)
        {
            var force = strength * 0.2;
            p1.VelocityX += (dx / distance) * force;
            p1.VelocityY += (dy / distance) * force;
            
            // Clamp velocities
            p1.VelocityX = Math.Clamp(p1.VelocityX, -5, 5);
            p1.VelocityY = Math.Clamp(p1.VelocityY, -5, 5);
            
            await _particleRepository.UpdateAsync(p1, cancellationToken);
        }
        
        // Publish event
        await PublishEventAsync(new ParticleInteractionEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            p1.Id,
            p2.Id,
            "Attract",
            strength
        ));
    }

    private async Task PublishEventAsync(BaseEvent @event)
    {
        try
        {
            var eventServiceUrl = _configuration["Services:EventService:Url"] ?? "https://localhost:5005";
            var httpClient = _httpClientFactory.CreateClient();
            
            string endpoint = @event switch
            {
                ParticleMergedEvent => "particle/merged",
                ParticleRepelledEvent => "particle/repelled",
                ParticleInteractionEvent => "particle/interaction",
                _ => null
            };
            
            if (endpoint != null)
            {
                await httpClient.PostAsJsonAsync($"{eventServiceUrl}/api/events/{endpoint}", @event);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish event {EventType}", @event.GetType().Name);
        }
    }
}
