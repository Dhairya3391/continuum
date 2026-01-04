using Microsoft.AspNetCore.Mvc;

namespace PersonalUniverse.VisualizationFeed.API.Controllers;

[ApiController]
[Route("/")]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "VisualizationFeed", timestamp = DateTime.UtcNow });
    }
}
