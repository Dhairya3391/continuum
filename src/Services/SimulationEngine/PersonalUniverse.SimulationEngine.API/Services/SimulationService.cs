using PersonalUniverse.Shared.Contracts.Interfaces;
using PersonalUniverse.Shared.Models.Entities;
using PersonalUniverse.Shared.Models.DTOs;
using PersonalUniverse.Shared.Contracts.Events;
using PersonalUniverse.Shared.Models.Mappers;

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

        var particleDtos = ParticleMapper.ToDtos(particleList).ToList();

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

            // Check if particle should split (high instability)
            if (particle.State == ParticleState.Active)
            {
                if (await ShouldSplitParticleAsync(particle, cancellationToken))
                {
                    await HandleSplitAsync(particle, cancellationToken);
                    _logger.LogInformation("Particle {ParticleId} split due to high instability", particle.Id);
                }
            }

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

    private async Task<bool> ShouldSplitParticleAsync(Particle particle, CancellationToken cancellationToken)
    {
        // Particles split when they have high internal conflict/instability
        // Conditions: high mass, high aggression, low stability, high energy variance
        
        if (particle.Mass < 2.0) return false; // Too small to split
        if (particle.Energy < 40) return false; // Not enough energy to sustain two particles
        
        var metrics = await _metricsRepository.GetLatestByParticleIdAsync(particle.Id, cancellationToken);
        if (metrics == null) return false;
        
        // High aggression + low stability = internal conflict
        var instability = metrics.Aggression * (1.0 - metrics.Stability);
        
        // Split threshold: instability > 0.6 and random chance
        if (instability > 0.6)
        {
            // 20% chance per tick when unstable
            var random = new Random();
            return random.NextDouble() < 0.2;
        }
        
        return false;
    }

    private async Task HandleSplitAsync(Particle sourceParticle, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Splitting particle {ParticleId} due to high instability", sourceParticle.Id);
        
        // Get source personality metrics
        var sourceMetrics = await _metricsRepository.GetLatestByParticleIdAsync(sourceParticle.Id, cancellationToken);
        if (sourceMetrics == null) return;
        
        // Create two new particles from the split
        var splitParticles = new List<Guid>();
        
        // Split mass and energy
        var splitMass = sourceParticle.Mass / 2.0;
        var splitEnergy = sourceParticle.Energy * 0.45; // Lose 10% energy in split
        
        // Create first split particle (more aggressive variant)
        var particle1 = new Particle
        {
            Id = Guid.NewGuid(),
            UserId = sourceParticle.UserId,
            PositionX = sourceParticle.PositionX + Random.Shared.Next(-10, 10),
            PositionY = sourceParticle.PositionY + Random.Shared.Next(-10, 10),
            VelocityX = sourceParticle.VelocityX + Random.Shared.NextDouble() * 2 - 1,
            VelocityY = sourceParticle.VelocityY + Random.Shared.NextDouble() * 2 - 1,
            Mass = splitMass,
            Energy = splitEnergy,
            State = ParticleState.Active,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            LastInputAt = sourceParticle.LastInputAt,
            DecayLevel = sourceParticle.DecayLevel
        };
        
        await _particleRepository.AddAsync(particle1, cancellationToken);
        splitParticles.Add(particle1.Id);
        
        // Create personality metrics for particle1 (emphasize aggression)
        var metrics1 = new PersonalityMetrics
        {
            Id = Guid.NewGuid(),
            ParticleId = particle1.Id,
            Curiosity = sourceMetrics.Curiosity * 1.1,
            SocialAffinity = sourceMetrics.SocialAffinity * 0.7, // Less social
            Aggression = Math.Min(1.0, sourceMetrics.Aggression * 1.3), // More aggressive
            Stability = sourceMetrics.Stability * 0.8, // Less stable
            GrowthPotential = sourceMetrics.GrowthPotential,
            CalculatedAt = DateTime.UtcNow,
            Version = sourceMetrics.Version + 1
        };
        await _metricsRepository.AddAsync(metrics1, cancellationToken);
        
        // Create second split particle (more stable variant)
        var particle2 = new Particle
        {
            Id = Guid.NewGuid(),
            UserId = sourceParticle.UserId,
            PositionX = sourceParticle.PositionX + Random.Shared.Next(-10, 10),
            PositionY = sourceParticle.PositionY + Random.Shared.Next(-10, 10),
            VelocityX = sourceParticle.VelocityX - (Random.Shared.NextDouble() * 2 - 1),
            VelocityY = sourceParticle.VelocityY - (Random.Shared.NextDouble() * 2 - 1),
            Mass = splitMass,
            Energy = splitEnergy,
            State = ParticleState.Active,
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow,
            LastInputAt = sourceParticle.LastInputAt,
            DecayLevel = sourceParticle.DecayLevel
        };
        
        await _particleRepository.AddAsync(particle2, cancellationToken);
        splitParticles.Add(particle2.Id);
        
        // Create personality metrics for particle2 (emphasize stability)
        var metrics2 = new PersonalityMetrics
        {
            Id = Guid.NewGuid(),
            ParticleId = particle2.Id,
            Curiosity = sourceMetrics.Curiosity * 0.9,
            SocialAffinity = Math.Min(1.0, sourceMetrics.SocialAffinity * 1.2), // More social
            Aggression = sourceMetrics.Aggression * 0.7, // Less aggressive
            Stability = Math.Min(1.0, sourceMetrics.Stability * 1.3), // More stable
            GrowthPotential = sourceMetrics.GrowthPotential,
            CalculatedAt = DateTime.UtcNow,
            Version = sourceMetrics.Version + 1
        };
        await _metricsRepository.AddAsync(metrics2, cancellationToken);
        
        // Mark source particle as expired
        sourceParticle.State = ParticleState.Expired;
        await _particleRepository.UpdateAsync(sourceParticle, cancellationToken);
        
        // Publish split event
        await PublishEventAsync(new ParticleSplitEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            sourceParticle.Id,
            splitParticles
        ));
        
        _logger.LogInformation(
            "Particle {SourceId} split into {Particle1} and {Particle2}",
            sourceParticle.Id, particle1.Id, particle2.Id
        );
    }

    private async Task PublishEventAsync(BaseEvent @event)
    {
        try
        {
            var eventServiceUrl = _configuration["Services:EventService:Url"] ?? "https://localhost:5005";
            var httpClient = _httpClientFactory.CreateClient();
            
            string? endpoint = @event switch
            {
                ParticleMergedEvent => "particle/merged",
                ParticleSplitEvent => "particle/split",
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
