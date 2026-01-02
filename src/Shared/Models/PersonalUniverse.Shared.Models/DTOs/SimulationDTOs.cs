namespace PersonalUniverse.Shared.Models.DTOs;

public record ParticleDto(
    Guid Id,
    Guid UserId,
    double PositionX,
    double PositionY,
    double VelocityX,
    double VelocityY,
    double Mass,
    double Energy,
    string State,
    int DecayLevel
);

public record ParticleEventDto(
    Guid Id,
    Guid ParticleId,
    Guid? TargetParticleId,
    string EventType,
    string Description,
    DateTime OccurredAt
);

public record UniverseStateDto(
    int TickNumber,
    DateTime Timestamp,
    int ActiveParticleCount,
    double AverageEnergy,
    int InteractionCount,
    List<ParticleDto> Particles
);

public record ParticleUpdateDto(
    Guid ParticleId,
    double PositionX,
    double PositionY,
    double VelocityX,
    double VelocityY,
    double Energy,
    string State
);
