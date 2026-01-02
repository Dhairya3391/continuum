using PersonalUniverse.Shared.Contracts.Interfaces;
using PersonalUniverse.Shared.Models.Entities;

namespace PersonalUniverse.SimulationEngine.API.Services;

public interface IInteractionService
{
    Task<InteractionResult> EvaluateInteractionAsync(Guid particle1Id, Guid particle2Id, CancellationToken cancellationToken = default);
    Task<double> CalculateCompatibilityAsync(Guid particle1Id, Guid particle2Id, CancellationToken cancellationToken = default);
}

public class InteractionResult
{
    public InteractionType Type { get; set; }
    public double Strength { get; set; }
    public string Description { get; set; } = string.Empty;
}

public enum InteractionType
{
    None,
    Attract,
    Repel,
    Merge,
    Bond
}

public class InteractionService : IInteractionService
{
    private readonly IParticleRepository _particleRepository;
    private readonly IPersonalityMetricsRepository _metricsRepository;
    private readonly ILogger<InteractionService> _logger;

    // Compatibility thresholds
    private const double MergeThreshold = 0.8;
    private const double BondThreshold = 0.6;
    private const double RepelThreshold = 0.3;

    public InteractionService(
        IParticleRepository particleRepository,
        IPersonalityMetricsRepository metricsRepository,
        ILogger<InteractionService> logger)
    {
        _particleRepository = particleRepository;
        _metricsRepository = metricsRepository;
        _logger = logger;
    }

    public async Task<InteractionResult> EvaluateInteractionAsync(Guid particle1Id, Guid particle2Id, CancellationToken cancellationToken = default)
    {
        var compatibility = await CalculateCompatibilityAsync(particle1Id, particle2Id, cancellationToken);

        if (compatibility >= MergeThreshold)
        {
            return new InteractionResult
            {
                Type = InteractionType.Merge,
                Strength = compatibility,
                Description = "High compatibility - particles merge"
            };
        }
        else if (compatibility >= BondThreshold)
        {
            return new InteractionResult
            {
                Type = InteractionType.Bond,
                Strength = compatibility,
                Description = "Moderate compatibility - particles bond"
            };
        }
        else if (compatibility >= RepelThreshold)
        {
            return new InteractionResult
            {
                Type = InteractionType.Attract,
                Strength = compatibility,
                Description = "Weak compatibility - particles attract slightly"
            };
        }
        else
        {
            return new InteractionResult
            {
                Type = InteractionType.Repel,
                Strength = 1.0 - compatibility,
                Description = "Low compatibility - particles repel"
            };
        }
    }

    public async Task<double> CalculateCompatibilityAsync(Guid particle1Id, Guid particle2Id, CancellationToken cancellationToken = default)
    {
        var particle1 = await _particleRepository.GetByIdAsync(particle1Id, cancellationToken);
        var particle2 = await _particleRepository.GetByIdAsync(particle2Id, cancellationToken);

        if (particle1 == null || particle2 == null)
        {
            return 0.0;
        }

        var metrics1 = await _metricsRepository.GetLatestByParticleIdAsync(particle1Id, cancellationToken);
        var metrics2 = await _metricsRepository.GetLatestByParticleIdAsync(particle2Id, cancellationToken);

        if (metrics1 == null || metrics2 == null)
        {
            // Default neutral compatibility
            return 0.5;
        }

        // Calculate compatibility based on personality metrics
        double compatibility = 0.0;

        // Social affinity correlation (high social + high social = good)
        var socialScore = 1.0 - Math.Abs(metrics1.SocialAffinity - metrics2.SocialAffinity);
        compatibility += socialScore * 0.3;

        // Curiosity correlation (similar curiosity levels work well together)
        var curiosityScore = 1.0 - Math.Abs(metrics1.Curiosity - metrics2.Curiosity);
        compatibility += curiosityScore * 0.2;

        // Aggression inverse correlation (low aggression + low aggression = good)
        var aggressionScore = 1.0 - ((metrics1.Aggression + metrics2.Aggression) / 2.0);
        compatibility += aggressionScore * 0.2;

        // Stability correlation (stable + stable = good)
        var stabilityScore = (metrics1.Stability + metrics2.Stability) / 2.0;
        compatibility += stabilityScore * 0.2;

        // Growth potential synergy
        var growthScore = Math.Sqrt(metrics1.GrowthPotential * metrics2.GrowthPotential);
        compatibility += growthScore * 0.1;

        _logger.LogDebug("Compatibility between {P1} and {P2}: {Score:F2}", 
            particle1Id, particle2Id, compatibility);

        return Math.Clamp(compatibility, 0.0, 1.0);
    }
}
