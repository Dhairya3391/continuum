namespace PersonalUniverse.Shared.Models.Entities;

public class DailyInput
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ParticleId { get; set; }
    public InputType Type { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public double? NumericValue { get; set; }
    public DateTime SubmittedAt { get; set; }
    public bool Processed { get; set; }
}

public enum InputType
{
    Mood,
    Energy,
    Intent,
    Preference,
    FreeText
}
