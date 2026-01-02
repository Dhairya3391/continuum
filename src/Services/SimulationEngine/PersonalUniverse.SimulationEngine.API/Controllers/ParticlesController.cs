using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PersonalUniverse.SimulationEngine.API.Services;
using PersonalUniverse.Shared.Models.DTOs;

namespace PersonalUniverse.SimulationEngine.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ParticlesController : ControllerBase
{
    private readonly IParticleService _particleService;
    private readonly ILogger<ParticlesController> _logger;

    public ParticlesController(IParticleService particleService, ILogger<ParticlesController> logger)
    {
        _particleService = particleService;
        _logger = logger;
    }

    [HttpPost("spawn/{userId}")]
    public async Task<IActionResult> SpawnParticle(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var particle = await _particleService.SpawnParticleAsync(userId, cancellationToken);
            return Ok(particle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error spawning particle for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to spawn particle" });
        }
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActiveParticles(CancellationToken cancellationToken)
    {
        var particles = await _particleService.GetActiveParticlesAsync(cancellationToken);
        return Ok(particles);
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetParticleByUserId(Guid userId, CancellationToken cancellationToken)
    {
        var particle = await _particleService.GetParticleByUserIdAsync(userId, cancellationToken);
        if (particle == null)
        {
            return NotFound(new { error = "Particle not found for user" });
        }
        return Ok(particle);
    }

    [HttpPut("update")]
    public async Task<IActionResult> UpdateParticle([FromBody] ParticleUpdateDto updateDto, CancellationToken cancellationToken)
    {
        var success = await _particleService.UpdateParticleStateAsync(updateDto, cancellationToken);
        if (!success)
        {
            return NotFound(new { error = "Particle not found" });
        }
        return Ok(new { message = "Particle updated successfully" });
    }
}
