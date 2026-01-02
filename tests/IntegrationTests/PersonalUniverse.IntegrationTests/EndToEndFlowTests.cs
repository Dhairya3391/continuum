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
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public EndToEndFlowTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CompleteUserFlow_RegisterToSimulation_ShouldSucceed()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Username = $"testuser_{Guid.NewGuid():N}",
            Email = $"test_{Guid.NewGuid():N}@example.com",
            Password = "TestPassword123!"
        };

        // Act & Assert Step 1: Register user
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerDto);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var registerResult = await registerResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        registerResult.Should().NotBeNull();
        registerResult!.Token.Should().NotBeNullOrEmpty();
        registerResult.UserId.Should().NotBeEmpty();

        // Set JWT token for authenticated requests
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", registerResult.Token);

        // Step 2: Spawn particle for user
        var spawnResponse = await _client.PostAsync($"/api/particles/spawn/{registerResult.UserId}", null);
        spawnResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var particle = await spawnResponse.Content.ReadFromJsonAsync<ParticleDto>();
        particle.Should().NotBeNull();
        particle!.UserId.Should().Be(registerResult.UserId);
        particle.State.Should().Be("Active");

        // Step 3: Submit daily input
        var inputDto = new DailyInputDto
        {
            UserId = registerResult.UserId,
            ParticleId = particle.Id,
            Question = "How are you feeling today?",
            Response = "I'm feeling curious and energetic!",
            Timestamp = DateTime.UtcNow
        };

        var inputResponse = await _client.PostAsJsonAsync("/api/personality/input", inputDto);
        inputResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var inputResult = await inputResponse.Content.ReadFromJsonAsync<ProcessingResultDto>();
        inputResult.Should().NotBeNull();
        inputResult!.Success.Should().BeTrue();

        // Step 4: Get personality metrics
        var metricsResponse = await _client.GetAsync($"/api/personality/metrics/{particle.Id}");
        metricsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var metrics = await metricsResponse.Content.ReadFromJsonAsync<PersonalityMetricsDto>();
        metrics.Should().NotBeNull();
        metrics!.ParticleId.Should().Be(particle.Id);
        metrics.Curiosity.Should().BeGreaterThan(0);

        // Step 5: Get active particles (should include our particle)
        var particlesResponse = await _client.GetAsync("/api/particles/active");
        particlesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var particles = await particlesResponse.Content.ReadFromJsonAsync<List<ParticleDto>>();
        particles.Should().NotBeNull();
        particles.Should().Contain(p => p.Id == particle.Id);

        // Step 6: Get user particle
        var userParticleResponse = await _client.GetAsync($"/api/particles/user/{registerResult.UserId}");
        userParticleResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var userParticle = await userParticleResponse.Content.ReadFromJsonAsync<ParticleDto>();
        userParticle.Should().NotBeNull();
        userParticle!.Id.Should().Be(particle.Id);
    }

    [Fact]
    public async Task UnauthenticatedRequest_ShouldReturn401()
    {
        // Arrange - no auth header set

        // Act
        var response = await _client.PostAsync($"/api/particles/spawn/{Guid.NewGuid()}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InvalidToken_ShouldReturn401()
    {
        // Arrange
        _client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", "invalid.jwt.token");

        // Act
        var response = await _client.PostAsync($"/api/particles/spawn/{Guid.NewGuid()}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

// DTOs for testing (match your actual DTOs)
public class RegisterDto
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
}

public class ProcessingResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
