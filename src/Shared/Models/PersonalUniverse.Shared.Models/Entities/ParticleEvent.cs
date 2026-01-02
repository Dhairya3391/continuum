namespace PersonalUniverse.Shared.Models.Entities;

public class ParticleEvent
{
    public Guid Id { get; set; }
    public Guid ParticleId { get; set; }
    public Guid? TargetParticleId { get; set; }
    public EventType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public enum EventType
{
    Spawned,
    Moved,
    Merged,
    Split,
    Bonded,
    Repelled,
    Decayed,
    Expired,
    EnergyChanged,
    InteractionOccurred
}
