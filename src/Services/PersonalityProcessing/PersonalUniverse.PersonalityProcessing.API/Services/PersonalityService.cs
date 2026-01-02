using PersonalUniverse.Shared.Contracts.Interfaces;
using PersonalUniverse.Shared.Models.Entities;
using PersonalUniverse.Shared.Models.DTOs;

namespace PersonalUniverse.PersonalityProcessing.API.Services;

public interface IPersonalityService
{
    Task<DailyInputResult> ProcessDailyInputAsync(DailyInputDto inputDto, CancellationToken cancellationToken = default);
    Task<PersonalityMetricsDto?> GetCurrentMetricsAsync(Guid particleId, CancellationToken cancellationToken = default);
    Task<int> GetDailyInputCountAsync(Guid userId, CancellationToken cancellationToken = default);
}

public class PersonalityService : IPersonalityService
{
    private readonly IParticleRepository _particleRepository;
    private readonly IPersonalityMetricsRepository _metricsRepository;
    private readonly IDailyInputRepository _inputRepository;
    private readonly ILogger<PersonalityService> _logger;
    private const int MaxDailyInputs = 3;

    public PersonalityService(
        IParticleRepository particleRepository,
        IPersonalityMetricsRepository metricsRepository,
        IDailyInputRepository inputRepository,
        ILogger<PersonalityService> logger)
    {
        _particleRepository = particleRepository;
        _metricsRepository = metricsRepository;
        _inputRepository = inputRepository;
        _logger = logger;
    }

    public async Task<DailyInputResult> ProcessDailyInputAsync(DailyInputDto inputDto, CancellationToken cancellationToken = default)
    {
        // Check rate limit
        var todayCount = await _inputRepository.GetDailyInputCountAsync(inputDto.UserId, DateTime.UtcNow, cancellationToken);
        if (todayCount >= MaxDailyInputs)
        {
            return new DailyInputResult(false, null, $"Daily input limit reached ({MaxDailyInputs} per day)");
        }

        // Get user's particle
        var particle = await _particleRepository.GetByUserIdAsync(inputDto.UserId, cancellationToken);
        if (particle == null)
        {
            return new DailyInputResult(false, null, "No particle found for user");
        }

        // Store the input
        var dailyInput = new DailyInput
        {
            UserId = inputDto.UserId,
            ParticleId = particle.Id,
            Type = Enum.Parse<InputType>(inputDto.InputType),
            Question = inputDto.Question,
            Response = inputDto.Response,
            NumericValue = inputDto.NumericValue,
            SubmittedAt = DateTime.UtcNow,
            Processed = false
        };

        await _inputRepository.AddAsync(dailyInput, cancellationToken);

        // Get current metrics
        var currentMetrics = await _metricsRepository.GetLatestByParticleIdAsync(particle.Id, cancellationToken);
        if (currentMetrics == null)
        {
            currentMetrics = new PersonalityMetrics
            {
                ParticleId = particle.Id,
                Curiosity = 0.5,
                SocialAffinity = 0.5,
                Aggression = 0.5,
                Stability = 0.5,
                GrowthPotential = 0.5,
                Version = 0
            };
        }

        // Process input and update metrics
        var updatedMetrics = CalculateMetricsFromInput(currentMetrics, dailyInput);
        updatedMetrics.Version = currentMetrics.Version + 1;
        await _metricsRepository.AddAsync(updatedMetrics, cancellationToken);

        // Update particle's last input time
        particle.LastInputAt = DateTime.UtcNow;
        particle.DecayLevel = Math.Max(0, particle.DecayLevel - 1);
        particle.Energy = Math.Min(100, particle.Energy + 5);
        await _particleRepository.UpdateAsync(particle, cancellationToken);

        // Mark input as processed
        dailyInput.Processed = true;
        await _inputRepository.UpdateAsync(dailyInput, cancellationToken);

        _logger.LogInformation("Processed input for user {UserId}, particle {ParticleId}", 
            inputDto.UserId, particle.Id);

        var metricsDto = new PersonalityMetricsDto(
            updatedMetrics.ParticleId,
            updatedMetrics.Curiosity,
            updatedMetrics.SocialAffinity,
            updatedMetrics.Aggression,
            updatedMetrics.Stability,
            updatedMetrics.GrowthPotential,
            updatedMetrics.CalculatedAt
        );

        return new DailyInputResult(true, metricsDto, "Input processed successfully");
    }

    public async Task<PersonalityMetricsDto?> GetCurrentMetricsAsync(Guid particleId, CancellationToken cancellationToken = default)
    {
        var metrics = await _metricsRepository.GetLatestByParticleIdAsync(particleId, cancellationToken);
        if (metrics == null)
        {
            return null;
        }

        return new PersonalityMetricsDto(
            metrics.ParticleId,
            metrics.Curiosity,
            metrics.SocialAffinity,
            metrics.Aggression,
            metrics.Stability,
            metrics.GrowthPotential,
            metrics.CalculatedAt
        );
    }

    public async Task<int> GetDailyInputCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _inputRepository.GetDailyInputCountAsync(userId, DateTime.UtcNow, cancellationToken);
    }

    private PersonalityMetrics CalculateMetricsFromInput(PersonalityMetrics current, DailyInput input)
    {
        var newMetrics = new PersonalityMetrics
        {
            ParticleId = current.ParticleId,
            Curiosity = current.Curiosity,
            SocialAffinity = current.SocialAffinity,
            Aggression = current.Aggression,
            Stability = current.Stability,
            GrowthPotential = current.GrowthPotential,
            CalculatedAt = DateTime.UtcNow
        };

        // Simple sentiment analysis and metric adjustment
        var sentiment = AnalyzeSentiment(input.Response);
        var intensity = input.NumericValue ?? 0.5;

        switch (input.Type)
        {
            case InputType.Mood:
                newMetrics.Stability = AdjustMetric(current.Stability, sentiment * intensity);
                newMetrics.GrowthPotential = AdjustMetric(current.GrowthPotential, sentiment * 0.5);
                break;

            case InputType.Energy:
                newMetrics.GrowthPotential = AdjustMetric(current.GrowthPotential, intensity);
                newMetrics.Curiosity = AdjustMetric(current.Curiosity, intensity * 0.3);
                break;

            case InputType.Intent:
                newMetrics.Curiosity = AdjustMetric(current.Curiosity, sentiment * intensity);
                newMetrics.SocialAffinity = AdjustMetric(current.SocialAffinity, sentiment * 0.4);
                break;

            case InputType.Preference:
                newMetrics.SocialAffinity = AdjustMetric(current.SocialAffinity, sentiment * intensity);
                break;

            case InputType.FreeText:
                // Free text affects multiple metrics slightly
                newMetrics.Curiosity = AdjustMetric(current.Curiosity, sentiment * 0.2);
                newMetrics.SocialAffinity = AdjustMetric(current.SocialAffinity, sentiment * 0.2);
                newMetrics.Stability = AdjustMetric(current.Stability, sentiment * 0.2);
                break;
        }

        return newMetrics;
    }

    private double AnalyzeSentiment(string text)
    {
        // Simple rule-based sentiment analysis
        var lowerText = text.ToLower();
        double score = 0.0;

        // Positive words
        string[] positiveWords = { "happy", "good", "great", "excited", "love", "excellent", "wonderful", "amazing", "joy", "better" };
        foreach (var word in positiveWords)
        {
            if (lowerText.Contains(word)) score += 0.2;
        }

        // Negative words
        string[] negativeWords = { "sad", "bad", "terrible", "hate", "awful", "angry", "frustrated", "tired", "worse", "difficult" };
        foreach (var word in negativeWords)
        {
            if (lowerText.Contains(word)) score -= 0.2;
        }

        // Social words increase social affinity
        string[] socialWords = { "people", "friend", "together", "connect", "share", "community", "talk", "meet" };
        foreach (var word in socialWords)
        {
            if (lowerText.Contains(word)) score += 0.1;
        }

        return Math.Clamp(score, -1.0, 1.0);
    }

    private double AdjustMetric(double current, double change)
    {
        // Smoothly adjust metric with diminishing returns
        var adjustment = change * 0.1; // Scale down the change
        var newValue = current + adjustment;
        return Math.Clamp(newValue, 0.0, 1.0);
    }
}
