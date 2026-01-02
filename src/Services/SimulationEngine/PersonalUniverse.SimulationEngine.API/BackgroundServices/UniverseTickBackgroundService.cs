using PersonalUniverse.SimulationEngine.API.Services;

namespace PersonalUniverse.SimulationEngine.API.BackgroundServices;

public class UniverseTickBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UniverseTickBackgroundService> _logger;
    private readonly TimeSpan _tickInterval = TimeSpan.FromSeconds(10); // Process universe every 10 seconds
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public UniverseTickBackgroundService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<UniverseTickBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Universe Tick Background Service is starting");

        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // Initial delay

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessUniverseTickAsync(stoppingToken);
                await Task.Delay(_tickInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during universe tick processing");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Wait longer on error
            }
        }

        _logger.LogInformation("Universe Tick Background Service is stopping");
    }

    private async Task ProcessUniverseTickAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var simulationService = scope.ServiceProvider.GetRequiredService<ISimulationService>();
        
        _logger.LogInformation("Processing universe tick at {Time}", DateTime.UtcNow);
        
        // Process simulation tick (movement, decay, interactions)
        await simulationService.ProcessSimulationTickAsync(cancellationToken);
        
        // Get current universe state
        var universeState = await simulationService.GetUniverseStateAsync(cancellationToken);
        
        // Broadcast to VisualizationFeed service
        await BroadcastUniverseStateAsync(universeState, cancellationToken);
        
        _logger.LogInformation("Universe tick completed: {ParticleCount} particles, tick {TickNumber}", 
            universeState.ActiveParticleCount, universeState.TickNumber);
    }

    private async Task BroadcastUniverseStateAsync(object universeState, CancellationToken cancellationToken)
    {
        try
        {
            var visualizationUrl = _configuration["Services:VisualizationFeed:Url"] ?? "https://localhost:5006";
            var response = await _httpClient.PostAsJsonAsync(
                $"{visualizationUrl}/api/broadcast/universe-state",
                universeState,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to broadcast universe state: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not connect to Visualization Feed service. Broadcast skipped.");
        }
    }
}
