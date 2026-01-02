using Microsoft.AspNetCore.SignalR;
using PersonalUniverse.Shared.Models.Entities;

namespace PersonalUniverse.VisualizationFeed.API.Hubs;

/// <summary>
/// SignalR hub for streaming universe state updates to connected clients
/// </summary>
public class UniverseHub : Hub
{
    private readonly ILogger<UniverseHub> _logger;

    public UniverseHub(ILogger<UniverseHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        
        // Add to default universe group
        await Groups.AddToGroupAsync(Context.ConnectionId, "universe:default");
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        
        if (exception != null)
        {
            _logger.LogError(exception, "Client disconnected with error");
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Join a specific universe stream
    /// </summary>
    public async Task JoinUniverse(string universeId)
    {
        var groupName = $"universe:{universeId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} joined universe {UniverseId}", Context.ConnectionId, universeId);
    }

    /// <summary>
    /// Leave a universe stream
    /// </summary>
    public async Task LeaveUniverse(string universeId)
    {
        var groupName = $"universe:{universeId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} left universe {UniverseId}", Context.ConnectionId, universeId);
    }

    /// <summary>
    /// Subscribe to a specific particle's updates
    /// </summary>
    public async Task FollowParticle(Guid particleId)
    {
        var groupName = $"particle:{particleId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} following particle {ParticleId}", Context.ConnectionId, particleId);
    }

    /// <summary>
    /// Unsubscribe from a particle's updates
    /// </summary>
    public async Task UnfollowParticle(Guid particleId)
    {
        var groupName = $"particle:{particleId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client {ConnectionId} unfollowed particle {ParticleId}", Context.ConnectionId, particleId);
    }
}
