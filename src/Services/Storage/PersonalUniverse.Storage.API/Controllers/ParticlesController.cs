using Microsoft.AspNetCore.Mvc;
using PersonalUniverse.Shared.Contracts.Interfaces;
using PersonalUniverse.Shared.Models.Entities;

namespace PersonalUniverse.Storage.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ParticlesController : ControllerBase
{
    private readonly IParticleRepository _particleRepository;
    private readonly ILogger<ParticlesController> _logger;

    public ParticlesController(IParticleRepository particleRepository, ILogger<ParticlesController> logger)
    {
        _particleRepository = particleRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var particles = await _particleRepository.GetAllAsync(cancellationToken);
        return Ok(particles);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var particle = await _particleRepository.GetByIdAsync(id, cancellationToken);
        if (particle == null)
        {
            return NotFound(new { error = "Particle not found" });
        }
        return Ok(particle);
    }

    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetByUserId(Guid userId, CancellationToken cancellationToken)
    {
        var particle = await _particleRepository.GetByUserIdAsync(userId, cancellationToken);
        if (particle == null)
        {
            return NotFound(new { error = "Particle not found for user" });
        }
        return Ok(particle);
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var particles = await _particleRepository.GetActiveParticlesAsync(cancellationToken);
        return Ok(particles);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Particle particle, CancellationToken cancellationToken)
    {
        var id = await _particleRepository.AddAsync(particle, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, particle);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Particle particle, CancellationToken cancellationToken)
    {
        particle.Id = id;
        var success = await _particleRepository.UpdateAsync(particle, cancellationToken);
        if (!success)
        {
            return NotFound(new { error = "Particle not found" });
        }
        return Ok(particle);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var success = await _particleRepository.DeleteAsync(id, cancellationToken);
        if (!success)
        {
            return NotFound(new { error = "Particle not found" });
        }
        return NoContent();
    }
}
