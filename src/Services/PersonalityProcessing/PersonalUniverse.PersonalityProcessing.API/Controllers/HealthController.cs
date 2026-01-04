using Microsoft.AspNetCore.Mvc;

namespace PersonalUniverse.PersonalityProcessing.API.Controllers;

[ApiController]
[Route("/")]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "PersonalityProcessing", timestamp = DateTime.UtcNow });
    }
}
