using PersonalUniverse.Shared.Contracts.Events;

namespace PersonalUniverse.EventService.API.Services;

public class NoOpEventPublisher : IEventPublisher
{
    private readonly ILogger<NoOpEventPublisher> _logger;

    public NoOpEventPublisher(ILogger<NoOpEventPublisher> logger)
    {
        _logger = logger;
        _logger.LogWarning("Using NoOpEventPublisher. Events will not be published.");
    }

    public Task PublishAsync<T>(T @event, string routingKey) where T : class
    {
        _logger.LogDebug("NoOp publish for {RoutingKey}", routingKey);
        return Task.CompletedTask;
    }

    public Task PublishParticleSpawnedAsync(ParticleSpawnedEvent @event) => PublishAsync(@event, "particle.spawned");
    public Task PublishParticleMergedAsync(ParticleMergedEvent @event) => PublishAsync(@event, "particle.merged");
    public Task PublishParticleRepelledAsync(ParticleRepelledEvent @event) => PublishAsync(@event, "particle.repelled");
    public Task PublishParticleSplitAsync(ParticleSplitEvent @event) => PublishAsync(@event, "particle.split");
    public Task PublishParticleExpiredAsync(ParticleExpiredEvent @event) => PublishAsync(@event, "particle.expired");
    public Task PublishDailyProcessingCompletedAsync(DailyProcessingCompletedEvent @event) => PublishAsync(@event, "daily.completed");
}
