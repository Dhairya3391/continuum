using Microsoft.AspNetCore.Mvc;

namespace PersonalUniverse.Storage.API.Controllers;

[ApiController]
[Route("/")]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "Storage", timestamp = DateTime.UtcNow });
    }
}
