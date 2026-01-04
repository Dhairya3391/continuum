using PersonalUniverse.EventService.API.Services;
using PersonalUniverse.Shared.Contracts.Events;

namespace PersonalUniverse.EventService.API.BackgroundServices;

public class EventConsumerBackgroundService : BackgroundService
{
    private readonly IEventSubscriber _eventSubscriber;
    private readonly ILogger<EventConsumerBackgroundService> _logger;

    public EventConsumerBackgroundService(
        IEventSubscriber eventSubscriber,
        ILogger<EventConsumerBackgroundService> logger)
    {
        _eventSubscriber = eventSubscriber;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Event Consumer Background Service starting...");

        try
        {
            // Subscribe to particle spawned events
            await _eventSubscriber.SubscribeAsync<ParticleSpawnedEvent>(
                "particle.spawned",
                async (@event) =>
                {
                    _logger.LogInformation(
                        "Particle spawned: {ParticleId} for User: {UserId} at ({X}, {Y})",
                        @event.ParticleId, @event.UserId, @event.InitialX, @event.InitialY);
                });

            // Subscribe to particle merged events
            await _eventSubscriber.SubscribeAsync<ParticleMergedEvent>(
                "particle.merged",
                async (@event) =>
                {
                    _logger.LogInformation(
                        "Particles merged: {Particle1} + {Particle2} = {Result}",
                        @event.SourceParticleId, @event.TargetParticleId, @event.ResultingParticleId);
                });

            // Subscribe to particle repelled events
            await _eventSubscriber.SubscribeAsync<ParticleRepelledEvent>(
                "particle.repelled",
                async (@event) =>
                {
                    _logger.LogInformation(
                        "Particles repelled: {Particle1} <-> {Particle2} with force {Force}",
                        @event.Particle1Id, @event.Particle2Id, @event.RepulsionForce);
                });

            // Subscribe to particle expired events
            await _eventSubscriber.SubscribeAsync<ParticleExpiredEvent>(
                "particle.expired",
                async (@event) =>
                {
                    _logger.LogInformation(
                        "Particle expired: {ParticleId}, Reason: {Reason}",
                        @event.ParticleId, @event.Reason);
                });

            // Subscribe to particle interaction events
            await _eventSubscriber.SubscribeAsync<ParticleInteractionEvent>(
                "particle.interaction",
                async (@event) =>
                {
                    _logger.LogInformation(
                        "Particle interaction: {Particle1} <-> {Particle2}, Type: {Type}, Strength: {Strength}",
                        @event.Particle1Id, @event.Particle2Id, @event.InteractionType, @event.ImpactStrength);
                });

            // Subscribe to personality updated events
            await _eventSubscriber.SubscribeAsync<PersonalityUpdatedEvent>(
                "personality.updated",
                async (@event) =>
                {
                    _logger.LogInformation(
                        "Personality updated for Particle: {ParticleId}",
                        @event.ParticleId);
                });

            // Subscribe to all events with wildcard
            await _eventSubscriber.SubscribeAsync(
                "particle.#",
                async (message) =>
                {
                    _logger.LogDebug("Received particle event: {Message}", message);
                });

            _logger.LogInformation("Event Consumer Background Service is listening for events...");

            // Keep service running
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Event Consumer Background Service");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Event Consumer Background Service stopping...");
        await base.StopAsync(cancellationToken);
    }
}
