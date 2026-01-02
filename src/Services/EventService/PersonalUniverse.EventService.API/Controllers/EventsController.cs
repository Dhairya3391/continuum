using Microsoft.AspNetCore.Mvc;
using PersonalUniverse.Shared.Contracts.Events;

namespace PersonalUniverse.EventService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<EventsController> _logger;

    public EventsController(
        IEventPublisher eventPublisher,
        ILogger<EventsController> logger)
    {
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    /// <summary>
    /// Publish a particle spawned event
    /// </summary>
    [HttpPost("particle/spawned")]
    public async Task<IActionResult> PublishParticleSpawned([FromBody] ParticleSpawnedEvent @event)
    {
        try
        {
            await _eventPublisher.PublishParticleSpawnedAsync(@event);
            return Ok(new { message = "Event published successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish particle spawned event");
            return StatusCode(500, new { error = "Failed to publish event" });
        }
    }

    /// <summary>
    /// Publish a particle merged event
    /// </summary>
    [HttpPost("particle/merged")]
    public async Task<IActionResult> PublishParticleMerged([FromBody] ParticleMergedEvent @event)
    {
        try
        {
            await _eventPublisher.PublishParticleMergedAsync(@event);
            return Ok(new { message = "Event published successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish particle merged event");
            return StatusCode(500, new { error = "Failed to publish event" });
        }
    }

    /// <summary>
    /// Publish a particle repelled event
    /// </summary>
    [HttpPost("particle/repelled")]
    public async Task<IActionResult> PublishParticleRepelled([FromBody] ParticleRepelledEvent @event)
    {
        try
        {
            await _eventPublisher.PublishParticleRepelledAsync(@event);
            return Ok(new { message = "Event published successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish particle repelled event");
            return StatusCode(500, new { error = "Failed to publish event" });
        }
    }

    /// <summary>
    /// Publish a particle split event
    /// </summary>
    [HttpPost("particle/split")]
    public async Task<IActionResult> PublishParticleSplit([FromBody] ParticleSplitEvent @event)
    {
        try
        {
            await _eventPublisher.PublishParticleSplitAsync(@event);
            return Ok(new { message = "Event published successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish particle split event");
            return StatusCode(500, new { error = "Failed to publish event" });
        }
    }

    /// <summary>
    /// Publish a particle expired event
    /// </summary>
    [HttpPost("particle/expired")]
    public async Task<IActionResult> PublishParticleExpired([FromBody] ParticleExpiredEvent @event)
    {
        try
        {
            await _eventPublisher.PublishParticleExpiredAsync(@event);
            return Ok(new { message = "Event published successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish particle expired event");
            return StatusCode(500, new { error = "Failed to publish event" });
        }
    }

    /// <summary>
    /// Publish a daily processing completed event
    /// </summary>
    [HttpPost("universe/daily/completed")]
    public async Task<IActionResult> PublishDailyProcessingCompleted([FromBody] DailyProcessingCompletedEvent @event)
    {
        try
        {
            await _eventPublisher.PublishDailyProcessingCompletedAsync(@event);
            return Ok(new { message = "Event published successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish daily processing completed event");
            return StatusCode(500, new { error = "Failed to publish event" });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "EventService", timestamp = DateTime.UtcNow });
    }
}
