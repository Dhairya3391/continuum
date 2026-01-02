namespace PersonalUniverse.Shared.Contracts.Events;

/// <summary>
/// Interface for publishing events to message queue
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, string routingKey) where T : class;
    Task PublishParticleSpawnedAsync(ParticleSpawnedEvent @event);
    Task PublishParticleMergedAsync(ParticleMergedEvent @event);
    Task PublishParticleRepelledAsync(ParticleRepelledEvent @event);
    Task PublishParticleSplitAsync(ParticleSplitEvent @event);
    Task PublishParticleExpiredAsync(ParticleExpiredEvent @event);
    Task PublishDailyProcessingCompletedAsync(DailyProcessingCompletedEvent @event);
}
