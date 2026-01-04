using Microsoft.AspNetCore.Mvc;

namespace PersonalUniverse.SimulationEngine.API.Controllers;

[ApiController]
[Route("/")]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "SimulationEngine", timestamp = DateTime.UtcNow });
    }
}
