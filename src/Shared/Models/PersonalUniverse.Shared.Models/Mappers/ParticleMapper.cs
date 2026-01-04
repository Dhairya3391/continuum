using PersonalUniverse.Shared.Models.DTOs;
using PersonalUniverse.Shared.Models.Entities;

namespace PersonalUniverse.Shared.Models.Mappers;

public static class ParticleMapper
{
    public static ParticleDto ToDto(Particle particle)
    {
        return new ParticleDto(
            particle.Id,
            particle.UserId,
            particle.PositionX,
            particle.PositionY,
            particle.VelocityX,
            particle.VelocityY,
            particle.Mass,
            particle.Energy,
            particle.State.ToString(),
            particle.DecayLevel
        );
    }

    public static IEnumerable<ParticleDto> ToDtos(IEnumerable<Particle> particles)
    {
        return particles.Select(ToDto);
    }
}
