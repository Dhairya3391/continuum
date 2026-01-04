using Microsoft.AspNetCore.Mvc;

namespace PersonalUniverse.EventService.API.Controllers;

[ApiController]
[Route("/")]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "EventService", timestamp = DateTime.UtcNow });
    }
}
