namespace PersonalUniverse.Shared.Models.Entities;

public class UniverseState
{
    public Guid Id { get; set; }
    public int TickNumber { get; set; }
    public DateTime Timestamp { get; set; }
    public int ActiveParticleCount { get; set; }
    public double AverageEnergy { get; set; }
    public int InteractionCount { get; set; }
    public string SnapshotData { get; set; } = string.Empty; // JSON serialized state
}
