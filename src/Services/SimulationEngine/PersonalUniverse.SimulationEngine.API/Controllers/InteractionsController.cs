using Microsoft.AspNetCore.Mvc;
using PersonalUniverse.SimulationEngine.API.Services;

namespace PersonalUniverse.SimulationEngine.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InteractionsController : ControllerBase
{
    private readonly IInteractionService _interactionService;
    private readonly ILogger<InteractionsController> _logger;

    public InteractionsController(IInteractionService interactionService, ILogger<InteractionsController> logger)
    {
        _interactionService = interactionService;
        _logger = logger;
    }

    /// <summary>
    /// Evaluate interaction between two particles
    /// </summary>
    [HttpGet("evaluate")]
    public async Task<IActionResult> EvaluateInteraction(
        [FromQuery] Guid particle1Id, 
        [FromQuery] Guid particle2Id, 
        CancellationToken cancellationToken)
    {
        if (particle1Id == Guid.Empty || particle2Id == Guid.Empty)
        {
            return BadRequest(new { error = "Both particle IDs are required" });
        }

        if (particle1Id == particle2Id)
        {
            return BadRequest(new { error = "Cannot evaluate interaction with itself" });
        }

        try
        {
            var result = await _interactionService.EvaluateInteractionAsync(particle1Id, particle2Id, cancellationToken);
            return Ok(new
            {
                particle1Id,
                particle2Id,
                interactionType = result.Type.ToString(),
                strength = result.Strength,
                description = result.Description
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating interaction between {P1} and {P2}", particle1Id, particle2Id);
            return StatusCode(500, new { error = "Failed to evaluate interaction" });
        }
    }

    /// <summary>
    /// Calculate compatibility score between two particles
    /// </summary>
    [HttpGet("compatibility")]
    public async Task<IActionResult> GetCompatibility(
        [FromQuery] Guid particle1Id, 
        [FromQuery] Guid particle2Id, 
        CancellationToken cancellationToken)
    {
        if (particle1Id == Guid.Empty || particle2Id == Guid.Empty)
        {
            return BadRequest(new { error = "Both particle IDs are required" });
        }

        try
        {
            var compatibility = await _interactionService.CalculateCompatibilityAsync(particle1Id, particle2Id, cancellationToken);
            return Ok(new
            {
                particle1Id,
                particle2Id,
                compatibility,
                percentage = $"{compatibility * 100:F1}%"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating compatibility between {P1} and {P2}", particle1Id, particle2Id);
            return StatusCode(500, new { error = "Failed to calculate compatibility" });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "Interactions" });
    }
}
