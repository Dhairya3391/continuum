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
        // Advanced sentiment analysis with weighted keywords, phrases, and context
        var lowerText = text.ToLower();
        double score = 0.0;
        int matchCount = 0;

        // Intensity modifiers (check first for context)
        var intensityMultiplier = 1.0;
        string[] intensifiers = { "very", "extremely", "really", "so", "absolutely", "completely", "totally", "incredibly" };
        string[] diminishers = { "slightly", "somewhat", "a bit", "kind of", "sort of", "barely", "hardly" };
        
        foreach (var intensifier in intensifiers)
        {
            if (lowerText.Contains(intensifier))
            {
                intensityMultiplier = 1.5;
                break;
            }
        }
        foreach (var diminisher in diminishers)
        {
            if (lowerText.Contains(diminisher))
            {
                intensityMultiplier = 0.6;
                break;
            }
        }

        // Negation detection (inverts sentiment)
        bool hasNegation = lowerText.Contains("not ") || lowerText.Contains("don't ") || 
                           lowerText.Contains("doesn't ") || lowerText.Contains("never ") ||
                           lowerText.Contains("no ") || lowerText.Contains("cannot ") || lowerText.Contains("can't ");

        // Multi-word phrases (check first, higher weight)
        var phrases = new Dictionary<string, double>
        {
            // Very positive phrases
            ["feel amazing"] = 1.0, ["feeling great"] = 0.9, ["so happy"] = 0.9,
            ["love it"] = 0.85, ["best day"] = 0.9, ["really excited"] = 0.85,
            ["can't wait"] = 0.8, ["looking forward"] = 0.75,
            
            // Very negative phrases
            ["feel terrible"] = -0.9, ["so sad"] = -0.85, ["hate it"] = -0.9,
            ["worst day"] = -0.95, ["giving up"] = -0.8, ["fed up"] = -0.75,
            ["can't take"] = -0.8, ["had enough"] = -0.7,
            
            // Social phrases
            ["with friends"] = 0.7, ["spending time"] = 0.65, ["want to connect"] = 0.75,
            ["meet people"] = 0.7, ["hanging out"] = 0.65, ["working together"] = 0.7,
            
            // Curiosity phrases
            ["want to learn"] = 0.8, ["interested in"] = 0.75, ["excited to explore"] = 0.85,
            ["figure out"] = 0.7, ["find out"] = 0.7, ["want to know"] = 0.75,
            
            // Competitive/aggressive phrases
            ["want to win"] = 0.7, ["beat them"] = 0.75, ["crush it"] = 0.65,
            ["take on"] = 0.6, ["bring it on"] = 0.7
        };

        foreach (var phrase in phrases)
        {
            if (lowerText.Contains(phrase.Key))
            {
                var phraseScore = phrase.Value * intensityMultiplier;
                score += hasNegation ? -phraseScore : phraseScore;
                matchCount++;
            }
        }

        // Weighted positive keywords (stronger = higher weight)
        var positiveKeywords = new Dictionary<string, double>
        {
            // Strong positive (0.8-1.0)
            ["ecstatic"] = 1.0, ["thrilled"] = 0.95, ["overjoyed"] = 0.95, ["elated"] = 0.9,
            ["euphoric"] = 0.95, ["blissful"] = 0.9, ["exhilarated"] = 0.9,
            
            // Medium positive (0.5-0.8)
            ["happy"] = 0.7, ["excited"] = 0.75, ["love"] = 0.8, ["excellent"] = 0.75,
            ["wonderful"] = 0.7, ["amazing"] = 0.75, ["fantastic"] = 0.75, ["brilliant"] = 0.7,
            ["great"] = 0.6, ["good"] = 0.5, ["joy"] = 0.7, ["delighted"] = 0.7,
            ["optimistic"] = 0.65, ["motivated"] = 0.7, ["energized"] = 0.7, ["inspired"] = 0.75,
            ["proud"] = 0.65, ["grateful"] = 0.7, ["thankful"] = 0.65, ["satisfied"] = 0.6,
            
            // Light positive (0.3-0.5)
            ["pleasant"] = 0.5, ["content"] = 0.5, ["okay"] = 0.3, ["fine"] = 0.35,
            ["better"] = 0.4, ["positive"] = 0.5, ["hopeful"] = 0.55
        };

        foreach (var keyword in positiveKeywords)
        {
            if (lowerText.Contains(keyword.Key))
            {
                var keywordScore = keyword.Value * intensityMultiplier;
                score += hasNegation ? -keywordScore : keywordScore;
                matchCount++;
            }
        }

        // Weighted negative keywords
        var negativeKeywords = new Dictionary<string, double>
        {
            // Strong negative (0.8-1.0)
            ["devastated"] = -1.0, ["miserable"] = -0.95, ["hopeless"] = -0.95, ["despair"] = -0.95,
            ["anguish"] = -0.9, ["tormented"] = -0.9, ["traumatic"] = -0.9,
            
            // Medium negative (0.5-0.8)
            ["depressed"] = -0.85, ["hate"] = -0.8, ["terrible"] = -0.75, ["awful"] = -0.75,
            ["horrible"] = -0.75, ["angry"] = -0.7, ["frustrated"] = -0.65, ["anxious"] = -0.65,
            ["stressed"] = -0.6, ["worried"] = -0.6, ["upset"] = -0.65, ["disappointed"] = -0.6,
            ["sad"] = -0.65, ["lonely"] = -0.7, ["exhausted"] = -0.6, ["overwhelmed"] = -0.65,
            
            // Light negative (0.3-0.5)
            ["tired"] = -0.4, ["bored"] = -0.45, ["difficult"] = -0.4, ["bad"] = -0.5,
            ["worse"] = -0.55, ["negative"] = -0.5, ["unhappy"] = -0.55
        };

        foreach (var keyword in negativeKeywords)
        {
            if (lowerText.Contains(keyword.Key))
            {
                var keywordScore = keyword.Value * intensityMultiplier;
                score += hasNegation ? -keywordScore : keywordScore;
                matchCount++;
            }
        }

        // Social keywords (weighted)
        var socialKeywords = new Dictionary<string, double>
        {
            ["friend"] = 0.8, ["friends"] = 0.8, ["love"] = 0.7, ["connect"] = 0.75,
            ["together"] = 0.7, ["community"] = 0.75, ["collaborate"] = 0.8, ["team"] = 0.7,
            ["people"] = 0.6, ["social"] = 0.65, ["share"] = 0.6, ["talk"] = 0.55,
            ["meet"] = 0.6, ["gathering"] = 0.65, ["party"] = 0.6, ["conversation"] = 0.7,
            ["relationship"] = 0.75, ["family"] = 0.7, ["network"] = 0.6, ["communicate"] = 0.65
        };

        foreach (var keyword in socialKeywords)
        {
            if (lowerText.Contains(keyword.Key))
            {
                score += keyword.Value * 0.5 * intensityMultiplier; // Social words have positive valence
                matchCount++;
            }
        }

        // Curiosity keywords (weighted)
        var curiosityKeywords = new Dictionary<string, double>
        {
            ["curious"] = 0.85, ["wonder"] = 0.8, ["explore"] = 0.85, ["discover"] = 0.9,
            ["learn"] = 0.8, ["study"] = 0.7, ["research"] = 0.75, ["investigate"] = 0.8,
            ["experiment"] = 0.85, ["question"] = 0.7, ["why"] = 0.6, ["how"] = 0.6,
            ["knowledge"] = 0.7, ["understand"] = 0.75, ["figure"] = 0.65, ["try"] = 0.6,
            ["new"] = 0.5, ["innovative"] = 0.75, ["creative"] = 0.75
        };

        foreach (var keyword in curiosityKeywords)
        {
            if (lowerText.Contains(keyword.Key))
            {
                score += keyword.Value * 0.5 * intensityMultiplier;
                matchCount++;
            }
        }

        // Aggressive/competitive keywords (weighted)
        var aggressiveKeywords = new Dictionary<string, double>
        {
            ["dominate"] = 0.9, ["conquer"] = 0.85, ["destroy"] = 0.9, ["crush"] = 0.85,
            ["compete"] = 0.7, ["fight"] = 0.75, ["win"] = 0.7, ["defeat"] = 0.8,
            ["challenge"] = 0.6, ["battle"] = 0.75, ["rival"] = 0.65, ["aggressive"] = 0.8,
            ["intense"] = 0.6, ["fierce"] = 0.7, ["attack"] = 0.85, ["powerful"] = 0.6
        };

        foreach (var keyword in aggressiveKeywords)
        {
            if (lowerText.Contains(keyword.Key))
            {
                score += keyword.Value * 0.4 * intensityMultiplier; // Moderate boost for aggression
                matchCount++;
            }
        }

        // Stability keywords (weighted)
        var stabilityKeywords = new Dictionary<string, double>
        {
            ["stable"] = 0.8, ["calm"] = 0.8, ["peaceful"] = 0.85, ["serene"] = 0.85,
            ["balanced"] = 0.8, ["steady"] = 0.75, ["consistent"] = 0.75, ["reliable"] = 0.7,
            ["grounded"] = 0.8, ["centered"] = 0.75, ["composed"] = 0.75, ["tranquil"] = 0.8
        };

        foreach (var keyword in stabilityKeywords)
        {
            if (lowerText.Contains(keyword.Key))
            {
                score += keyword.Value * 0.5 * intensityMultiplier;
                matchCount++;
            }
        }

        // Normalize based on text length to prevent spam
        var wordCount = lowerText.Split(new[] { ' ', ',', '.', '!', '?', ';', ':' }, 
                                         StringSplitOptions.RemoveEmptyEntries).Length;
        if (wordCount > 0 && matchCount > 0)
        {
            // Reduce impact if too many keywords relative to text length
            var keywordDensity = (double)matchCount / wordCount;
            if (keywordDensity > 0.5) // More than 50% keywords might be spam
            {
                score *= 0.7;
            }
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
