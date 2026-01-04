using PersonalUniverse.SimulationEngine.API.Services;
using PersonalUniverse.Shared.Contracts.Interfaces;
using PersonalUniverse.Shared.Models.Entities;

namespace PersonalUniverse.SimulationEngine.API.Jobs;

public class SimulationJobs
{
    private readonly ISimulationService _simulationService;
    private readonly IParticleRepository _particleRepository;
    private readonly ILogger<SimulationJobs> _logger;

    public SimulationJobs(
        ISimulationService simulationService,
        IParticleRepository particleRepository,
        ILogger<SimulationJobs> logger)
    {
        _simulationService = simulationService;
        _particleRepository = particleRepository;
        _logger = logger;
    }

    /// <summary>
    /// Runs the daily simulation tick - processes all particle movements and interactions
    /// </summary>
    public async Task ProcessDailyTickAsync()
    {
        try
        {
            _logger.LogInformation("Starting daily simulation tick at {Time}", DateTime.UtcNow);
            await _simulationService.ProcessSimulationTickAsync();
            _logger.LogInformation("Completed daily simulation tick at {Time}", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during daily simulation tick");
            throw;
        }
    }

    /// <summary>
    /// Processes particle decay - expires particles that haven't received input in 24 hours
    /// </summary>
    public async Task ProcessParticleDecayAsync()
    {
        try
        {
            _logger.LogInformation("Starting particle decay processing at {Time}", DateTime.UtcNow);
            
            var activeParticles = await _particleRepository.GetActiveParticlesAsync();
            var decayThreshold = DateTime.UtcNow.AddHours(-24);
            var decayedCount = 0;

            foreach (var particle in activeParticles)
            {
                if (particle.LastInputAt < decayThreshold && particle.State == ParticleState.Active)
                {
                    particle.State = ParticleState.Decaying;
                    particle.DecayLevel += 10;

                    if (particle.DecayLevel >= 100)
                    {
                        particle.State = ParticleState.Expired;
                    }

                    await _particleRepository.UpdateAsync(particle);
                    decayedCount++;
                }
            }

            _logger.LogInformation("Processed decay for {Count} particles at {Time}", decayedCount, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during particle decay processing");
            throw;
        }
    }

    /// <summary>
    /// Cleans up expired particles older than 30 days
    /// </summary>
    public async Task CleanupExpiredParticlesAsync()
    {
        try
        {
            _logger.LogInformation("Starting cleanup of expired particles at {Time}", DateTime.UtcNow);
            
            var allParticles = await _particleRepository.GetAllAsync();
            var expiredParticles = allParticles.Where(p => p.State == ParticleState.Expired);
            var cleanupThreshold = DateTime.UtcNow.AddDays(-30);
            var deletedCount = 0;

            foreach (var particle in expiredParticles)
            {
                if (particle.LastUpdatedAt < cleanupThreshold)
                {
                    await _particleRepository.DeleteAsync(particle.Id);
                    deletedCount++;
                }
            }

            _logger.LogInformation("Cleaned up {Count} expired particles at {Time}", deletedCount, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during expired particle cleanup");
            throw;
        }
    }
}
