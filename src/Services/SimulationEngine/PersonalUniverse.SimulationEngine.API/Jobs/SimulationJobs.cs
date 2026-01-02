using PersonalUniverse.SimulationEngine.API.Services;

namespace PersonalUniverse.SimulationEngine.API.Jobs;

public class SimulationJobs
{
    private readonly ISimulationService _simulationService;
    private readonly ILogger<SimulationJobs> _logger;

    public SimulationJobs(ISimulationService simulationService, ILogger<SimulationJobs> logger)
    {
        _simulationService = simulationService;
        _logger = logger;
    }

    /// <summary>
    /// Runs the simulation tick - processes all particle movements and interactions
    /// </summary>
    public async Task ProcessSimulationTick()
    {
        try
        {
            _logger.LogInformation("Starting scheduled simulation tick at {Time}", DateTime.UtcNow);
            await _simulationService.ProcessSimulationTickAsync();
            _logger.LogInformation("Completed scheduled simulation tick at {Time}", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scheduled simulation tick");
        }
    }
}
