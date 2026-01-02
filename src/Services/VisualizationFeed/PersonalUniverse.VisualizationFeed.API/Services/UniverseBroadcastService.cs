using Microsoft.AspNetCore.SignalR;
using PersonalUniverse.Shared.Models.Entities;
using PersonalUniverse.VisualizationFeed.API.Hubs;

namespace PersonalUniverse.VisualizationFeed.API.Services;

/// <summary>
/// Service for broadcasting universe updates to connected SignalR clients
/// </summary>
public class UniverseBroadcastService
{
    private readonly IHubContext<UniverseHub> _hubContext;
    private readonly ILogger<UniverseBroadcastService> _logger;

    public UniverseBroadcastService(
        IHubContext<UniverseHub> hubContext,
        ILogger<UniverseBroadcastService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Broadcast universe state to all connected clients
    /// </summary>
    public async Task BroadcastUniverseStateAsync(UniverseState state, string universeId = "default")
    {
        try
        {
            var groupName = $"universe:{universeId}";
            await _hubContext.Clients.Group(groupName).SendAsync("UniverseStateUpdate", state);
            
            _logger.LogDebug("Broadcasted universe state to {UniverseId}, tick {Tick}", 
                universeId, state.TickNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast universe state");
        }
    }

    /// <summary>
    /// Broadcast particle update to clients following that particle
    /// </summary>
    public async Task BroadcastParticleUpdateAsync(Particle particle)
    {
        try
        {
            var groupName = $"particle:{particle.Id}";
            await _hubContext.Clients.Group(groupName).SendAsync("ParticleUpdate", particle);
            
            _logger.LogDebug("Broadcasted update for particle {ParticleId}", particle.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast particle update");
        }
    }

    /// <summary>
    /// Broadcast list of active particles
    /// </summary>
    public async Task BroadcastActiveParticlesAsync(List<Particle> particles, string universeId = "default")
    {
        try
        {
            var groupName = $"universe:{universeId}";
            await _hubContext.Clients.Group(groupName).SendAsync("ActiveParticlesUpdate", particles);
            
            _logger.LogDebug("Broadcasted {Count} active particles to {UniverseId}", 
                particles.Count, universeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast active particles");
        }
    }

    /// <summary>
    /// Broadcast particle event notification
    /// </summary>
    public async Task BroadcastParticleEventAsync(object eventData, string eventType, string universeId = "default")
    {
        try
        {
            var groupName = $"universe:{universeId}";
            await _hubContext.Clients.Group(groupName).SendAsync("ParticleEvent", new
            {
                Type = eventType,
                Timestamp = DateTime.UtcNow,
                Data = eventData
            });
            
            _logger.LogInformation("Broadcasted {EventType} event to {UniverseId}", eventType, universeId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast particle event");
        }
    }

    /// <summary>
    /// Broadcast simulation metrics
    /// </summary>
    public async Task BroadcastSimulationMetricsAsync(object metrics, string universeId = "default")
    {
        try
        {
            var groupName = $"universe:{universeId}";
            await _hubContext.Clients.Group(groupName).SendAsync("SimulationMetrics", metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast simulation metrics");
        }
    }
}
