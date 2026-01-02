namespace PersonalUniverse.Shared.Contracts.Events;

public abstract record BaseEvent(
    Guid EventId,
    DateTime Timestamp
);

public record ParticleSpawnedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid ParticleId,
    Guid UserId,
    double InitialX,
    double InitialY
) : BaseEvent(EventId, Timestamp);

public record ParticleMergedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid SourceParticleId,
    Guid TargetParticleId,
    Guid ResultingParticleId
) : BaseEvent(EventId, Timestamp);

public record ParticleSplitEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid SourceParticleId,
    List<Guid> ResultingParticleIds
) : BaseEvent(EventId, Timestamp);

public record ParticleExpiredEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid ParticleId,
    string Reason
) : BaseEvent(EventId, Timestamp);

public record ParticleInteractionEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid Particle1Id,
    Guid Particle2Id,
    string InteractionType,
    double ImpactStrength
) : BaseEvent(EventId, Timestamp);

public record PersonalityUpdatedEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid ParticleId,
    Guid UserId,
    Dictionary<string, double> UpdatedMetrics
) : BaseEvent(EventId, Timestamp);

public record ParticleRepelledEvent(
    Guid EventId,
    DateTime Timestamp,
    Guid Particle1Id,
    Guid Particle2Id,
    double RepulsionForce
) : BaseEvent(EventId, Timestamp);

public record DailyProcessingCompletedEvent(
    Guid EventId,
    DateTime Timestamp,
    int TickNumber,
    int ProcessedParticles,
    int ActiveParticles,
    int ExpiredParticles
) : BaseEvent(EventId, Timestamp);
