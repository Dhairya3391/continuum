using Microsoft.AspNetCore.Mvc;

namespace PersonalUniverse.VisualizationFeed.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StreamController : ControllerBase
{
    private readonly ILogger<StreamController> _logger;

    public StreamController(ILogger<StreamController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get SignalR connection information
    /// </summary>
    [HttpGet("info")]
    public IActionResult GetStreamInfo()
    {
        var info = new
        {
            hubUrl = "/hubs/universe",
            supportedMethods = new[]
            {
                "UniverseStateUpdate",
                "ParticleUpdate",
                "ActiveParticlesUpdate",
                "ParticleEvent",
                "SimulationMetrics"
            },
            clientMethods = new[]
            {
                "JoinUniverse",
                "LeaveUniverse",
                "FollowParticle",
                "UnfollowParticle"
            }
        };

        return Ok(info);
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "VisualizationFeed", timestamp = DateTime.UtcNow });
    }
}
