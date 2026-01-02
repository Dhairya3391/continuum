using Microsoft.AspNetCore.Mvc;
using PersonalUniverse.PersonalityProcessing.API.Services;
using PersonalUniverse.Shared.Models.DTOs;

namespace PersonalUniverse.PersonalityProcessing.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PersonalityController : ControllerBase
{
    private readonly IPersonalityService _personalityService;
    private readonly ILogger<PersonalityController> _logger;

    public PersonalityController(IPersonalityService personalityService, ILogger<PersonalityController> logger)
    {
        _personalityService = personalityService;
        _logger = logger;
    }

    [HttpPost("input")]
    public async Task<IActionResult> SubmitInput([FromBody] DailyInputDto inputDto, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputDto.Question) || string.IsNullOrWhiteSpace(inputDto.Response))
        {
            return BadRequest(new { error = "Question and response are required" });
        }

        var result = await _personalityService.ProcessDailyInputAsync(inputDto, cancellationToken);
        
        if (!result.Success)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(result);
    }

    [HttpGet("metrics/{particleId}")]
    public async Task<IActionResult> GetMetrics(Guid particleId, CancellationToken cancellationToken)
    {
        var metrics = await _personalityService.GetCurrentMetricsAsync(particleId, cancellationToken);
        
        if (metrics == null)
        {
            return NotFound(new { error = "No metrics found for particle" });
        }

        return Ok(metrics);
    }

    [HttpGet("input-count/{userId}")]
    public async Task<IActionResult> GetDailyInputCount(Guid userId, CancellationToken cancellationToken)
    {
        var count = await _personalityService.GetDailyInputCountAsync(userId, cancellationToken);
        return Ok(new { userId, count, maxAllowed = 3, remaining = Math.Max(0, 3 - count) });
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", service = "Personality Processing" });
    }
}
