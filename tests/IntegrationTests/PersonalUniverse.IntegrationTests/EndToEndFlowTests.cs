using FluentAssertions;
using PersonalUniverse.IntegrationTests.Infrastructure;
using PersonalUniverse.Shared.Models.DTOs;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalUniverse.IntegrationTests;

public class EndToEndFlowTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _identityClient;
    private readonly HttpClient _simulationClient;
    private readonly HttpClient _personalityClient;
    private readonly IntegrationTestFactory _factory;
    private static readonly bool IntegrationEnabled =
        string.Equals(Environment.GetEnvironmentVariable("RUN_FULL_INTEGRATION"), "true", StringComparison.OrdinalIgnoreCase);

    public EndToEndFlowTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _identityClient = factory.CreateClient();

        // Resolve service URLs from env (double-underscore form) with sensible localhost defaults
        var simulationUrl = GetEnvOr("Services__SimulationEngine__Url", "http://localhost:5004");
        var personalityUrl = GetEnvOr("Services__PersonalityProcessing__Url", "http://localhost:5003");

        _simulationClient = new HttpClient { BaseAddress = new Uri(simulationUrl) };
        _personalityClient = new HttpClient { BaseAddress = new Uri(personalityUrl) };
    }

    private static string GetEnvOr(string key, string fallback)
    {
        return Environment.GetEnvironmentVariable(key) ?? fallback;
    }

    [Fact]
    public async Task CompleteUserFlow_RegisterToSimulation_ShouldSucceed()
    {
        if (!IntegrationEnabled) return;

        // Arrange
        var registerDto = new RegisterRequest
        {
            Username = $"testuser_{Guid.NewGuid():N}",
            Email = $"test_{Guid.NewGuid():N}@example.com",
            Password = "TestPassword123!"
        };

        // Act & Assert Step 1: Register user
        var registerResponse = await _identityClient.PostAsJsonAsync("/api/auth/register", registerDto);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var registerResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        registerResult.Should().NotBeNull();
        registerResult!.Token.Should().NotBeNullOrEmpty();
        registerResult.User.Should().NotBeNull();
        var userId = registerResult.User!.Id;
        userId.Should().NotBeEmpty();

        // Set JWT token for authenticated requests
        _simulationClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", registerResult.Token);
        _personalityClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", registerResult.Token);

        // Step 2: Spawn particle for user
        var spawnResponse = await _simulationClient.PostAsync($"/api/particles/spawn/{userId}", null);
        spawnResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var particle = await spawnResponse.Content.ReadFromJsonAsync<ParticleDto>();
        particle.Should().NotBeNull();
        particle!.UserId.Should().Be(userId);
        particle.State.Should().Be("Active");

        // Step 3: Submit daily input
        var inputDto = new DailyInputDto(
            UserId: userId,
            InputType: "FreeText",
            Question: "How are you feeling today?",
            Response: "I'm feeling curious and energetic!",
            NumericValue: null
        );

        var inputResponse = await _personalityClient.PostAsJsonAsync("/api/personality/input", inputDto);
        inputResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var inputResult = await inputResponse.Content.ReadFromJsonAsync<ProcessingResultDto>();
        inputResult.Should().NotBeNull();
        inputResult!.Success.Should().BeTrue();

        // Step 4: Get personality metrics
        var metricsResponse = await _personalityClient.GetAsync($"/api/personality/metrics/{particle.Id}");
        metricsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var metrics = await metricsResponse.Content.ReadFromJsonAsync<PersonalityMetricsDto>();
        metrics.Should().NotBeNull();
        metrics!.ParticleId.Should().Be(particle.Id);
        metrics.Curiosity.Should().BeGreaterThan(0);

        // Step 5: Get active particles (should include our particle)
        var particlesResponse = await _simulationClient.GetAsync("/api/particles/active");
        particlesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var particles = await particlesResponse.Content.ReadFromJsonAsync<List<ParticleDto>>();
        particles.Should().NotBeNull();
        particles.Should().Contain(p => p.Id == particle.Id);

        // Step 6: Get user particle
        var userParticleResponse = await _simulationClient.GetAsync($"/api/particles/user/{userId}");
        userParticleResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var userParticle = await userParticleResponse.Content.ReadFromJsonAsync<ParticleDto>();
        userParticle.Should().NotBeNull();
        userParticle!.Id.Should().Be(particle.Id);
    }

    [Fact]
    public async Task UnauthenticatedRequest_ShouldReturn401()
    {
        if (!IntegrationEnabled) return;

        // Arrange - no auth header set
        _simulationClient.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await _simulationClient.PostAsync($"/api/particles/spawn/{Guid.NewGuid()}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InvalidToken_ShouldReturn401()
    {
        if (!IntegrationEnabled) return;

        // Arrange
        _simulationClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

        // Act
        var response = await _simulationClient.PostAsync($"/api/particles/spawn/{Guid.NewGuid()}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

// DTOs aligned to real API responses
public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthUser
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public AuthUser? User { get; set; }
}

public class ProcessingResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
