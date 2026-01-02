using Microsoft.AspNetCore.Mvc;
using PersonalUniverse.SimulationEngine.API.Services;

namespace PersonalUniverse.SimulationEngine.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimulationController : ControllerBase
{
    private readonly ISimulationService _simulationService;
    private readonly ILogger<SimulationController> _logger;

    public SimulationController(ISimulationService simulationService, ILogger<SimulationController> logger)
    {
        _simulationService = simulationService;
        _logger = logger;
    }

    [HttpGet("state")]
    public async Task<IActionResult> GetUniverseState(CancellationToken cancellationToken)
    {
        var state = await _simulationService.GetUniverseStateAsync(cancellationToken);
        return Ok(state);
    }

    [HttpPost("tick")]
    public async Task<IActionResult> ProcessTick(CancellationToken cancellationToken)
    {
        try
        {
            await _simulationService.ProcessSimulationTickAsync(cancellationToken);
            return Ok(new { message = "Simulation tick processed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing simulation tick");
            return StatusCode(500, new { error = "Failed to process simulation tick" });
        }
    }

    [HttpGet("neighbors/{particleId}")]
    public async Task<IActionResult> GetNeighbors(Guid particleId, [FromQuery] double radius = 50.0, CancellationToken cancellationToken = default)
    {
        var neighbors = await _simulationService.FindNeighborsAsync(particleId, radius, cancellationToken);
        return Ok(neighbors);
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "Simulation Engine" });
    }
}
