namespace PersonalUniverse.Shared.Models.Entities;

public class Particle
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double VelocityX { get; set; }
    public double VelocityY { get; set; }
    public double Mass { get; set; }
    public double Energy { get; set; }
    public ParticleState State { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public DateTime? LastInputAt { get; set; }
    public int DecayLevel { get; set; }
}

public enum ParticleState
{
    Active,
    Decaying,
    Merging,
    Splitting,
    Expired
}
