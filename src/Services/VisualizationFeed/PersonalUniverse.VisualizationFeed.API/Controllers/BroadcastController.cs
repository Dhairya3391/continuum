using Microsoft.AspNetCore.Mvc;
using PersonalUniverse.VisualizationFeed.API.Services;
using PersonalUniverse.Shared.Models.Entities;
using PersonalUniverse.Shared.Models.DTOs;

namespace PersonalUniverse.VisualizationFeed.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BroadcastController : ControllerBase
{
    private readonly UniverseBroadcastService _broadcastService;
    private readonly ILogger<BroadcastController> _logger;

    public BroadcastController(
        UniverseBroadcastService broadcastService,
        ILogger<BroadcastController> logger)
    {
        _broadcastService = broadcastService;
        _logger = logger;
    }

    /// <summary>
    /// Receive universe state from SimulationEngine and broadcast to clients
    /// </summary>
    [HttpPost("universe-state")]
    public async Task<IActionResult> BroadcastUniverseState([FromBody] UniverseStateDto state)
    {
        try
        {
            // Convert DTO particles to entities for broadcast
            var particleEntities = state.Particles.Select(p => new Particle
            {
                Id = p.Id,
                UserId = p.UserId,
                PositionX = p.PositionX,
                PositionY = p.PositionY,
                VelocityX = p.VelocityX,
                VelocityY = p.VelocityY,
                Mass = p.Mass,
                Energy = p.Energy,
                State = Enum.Parse<ParticleState>(p.State),
                DecayLevel = p.DecayLevel
            }).ToList();

            await _broadcastService.BroadcastActiveParticlesAsync(particleEntities);
            
            return Ok(new { message = "Universe state broadcasted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast universe state");
            return StatusCode(500, new { error = "Failed to broadcast universe state" });
        }
    }

    /// <summary>
    /// Receive particle update and broadcast to followers
    /// </summary>
    [HttpPost("particle-update")]
    public async Task<IActionResult> BroadcastParticleUpdate([FromBody] ParticleDto particle)
    {
        try
        {
            var particleEntity = new Particle
            {
                Id = particle.Id,
                UserId = particle.UserId,
                PositionX = particle.PositionX,
                PositionY = particle.PositionY,
                VelocityX = particle.VelocityX,
                VelocityY = particle.VelocityY,
                Mass = particle.Mass,
                Energy = particle.Energy,
                State = Enum.Parse<ParticleState>(particle.State),
                DecayLevel = particle.DecayLevel
            };

            await _broadcastService.BroadcastParticleUpdateAsync(particleEntity);
            
            return Ok(new { message = "Particle update broadcasted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast particle update");
            return StatusCode(500, new { error = "Failed to broadcast particle update" });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "Broadcast" });
    }
}
