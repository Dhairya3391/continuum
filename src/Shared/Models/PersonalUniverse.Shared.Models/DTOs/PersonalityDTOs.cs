namespace PersonalUniverse.Shared.Models.DTOs;

public record DailyInputDto(
    Guid UserId,
    string InputType,
    string Question,
    string Response,
    double? NumericValue = null
);

public record PersonalityMetricsDto(
    Guid ParticleId,
    double Curiosity,
    double SocialAffinity,
    double Aggression,
    double Stability,
    double GrowthPotential,
    DateTime CalculatedAt
);

public record DailyInputResult(
    bool Success,
    PersonalityMetricsDto? UpdatedMetrics,
    string? Message
);
