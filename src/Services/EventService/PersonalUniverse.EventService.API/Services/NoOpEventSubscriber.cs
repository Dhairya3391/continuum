using PersonalUniverse.Shared.Contracts.Events;

namespace PersonalUniverse.EventService.API.Services;

public class NoOpEventSubscriber : IEventSubscriber
{
    private readonly ILogger<NoOpEventSubscriber> _logger;

    public NoOpEventSubscriber(ILogger<NoOpEventSubscriber> logger)
    {
        _logger = logger;
        _logger.LogWarning("Using NoOpEventSubscriber. Events will not be consumed.");
    }

    public Task SubscribeAsync(string routingKey, Func<string, Task> handler)
    {
        _logger.LogDebug("NoOp subscribe for {RoutingKey}", routingKey);
        return Task.CompletedTask;
    }

    public Task SubscribeAsync<T>(string routingKey, Func<T, Task> handler) where T : class
    {
        _logger.LogDebug("NoOp subscribe for {RoutingKey}", routingKey);
        return Task.CompletedTask;
    }
}
