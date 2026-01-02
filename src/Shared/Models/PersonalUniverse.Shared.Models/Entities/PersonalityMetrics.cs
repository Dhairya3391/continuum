namespace PersonalUniverse.Shared.Models.Entities;

public class PersonalityMetrics
{
    public Guid Id { get; set; }
    public Guid ParticleId { get; set; }
    public double Curiosity { get; set; }
    public double SocialAffinity { get; set; }
    public double Aggression { get; set; }
    public double Stability { get; set; }
    public double GrowthPotential { get; set; }
    public DateTime CalculatedAt { get; set; }
    public int Version { get; set; }
}
