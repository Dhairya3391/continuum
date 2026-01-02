using RabbitMQ.Client;
using PersonalUniverse.Shared.Contracts.Events;
using System.Text;
using System.Text.Json;

namespace PersonalUniverse.SimulationEngine.API.Services;

public class RabbitMqSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "personal_universe_events";
}

public interface ISimulationEventPublisher
{
    Task PublishParticleSpawnedAsync(Guid particleId, Guid userId, double x, double y);
    Task PublishInteractionAsync(Guid particle1Id, Guid particle2Id, string interactionType, double strength);
    Task PublishParticleExpiredAsync(Guid particleId, string reason);
}

public class SimulationEventPublisher : ISimulationEventPublisher, IDisposable
{
    private readonly IConnection? _connection;
    private readonly IModel? _channel;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<SimulationEventPublisher> _logger;
    private readonly bool _isConnected;

    public SimulationEventPublisher(
        RabbitMqSettings settings, 
        ILogger<SimulationEventPublisher> logger)
    {
        _settings = settings;
        _logger = logger;

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = settings.Host,
                Port = settings.Port,
                UserName = settings.Username,
                Password = settings.Password,
                VirtualHost = settings.VirtualHost
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare(
                exchange: settings.ExchangeName,
                type: "topic",
                durable: true,
                autoDelete: false
            );

            _isConnected = true;
            _logger.LogInformation("SimulationEventPublisher connected to RabbitMQ at {Host}:{Port}", 
                settings.Host, settings.Port);
        }
        catch (Exception ex)
        {
            _isConnected = false;
            _logger.LogWarning(ex, "Failed to connect to RabbitMQ. Events will not be published. " +
                "Ensure RabbitMQ is running at {Host}:{Port}", settings.Host, settings.Port);
        }
    }

    public async Task PublishParticleSpawnedAsync(Guid particleId, Guid userId, double x, double y)
    {
        if (!_isConnected || _channel == null) return;

        var @event = new ParticleSpawnedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            particleId,
            userId,
            x,
            y
        );

        await PublishEventAsync(@event, "particle.spawned");
    }

    public async Task PublishInteractionAsync(Guid particle1Id, Guid particle2Id, string interactionType, double strength)
    {
        if (!_isConnected || _channel == null) return;

        var @event = new ParticleInteractionEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            particle1Id,
            particle2Id,
            interactionType,
            strength
        );

        await PublishEventAsync(@event, "particle.interaction");
    }

    public async Task PublishParticleExpiredAsync(Guid particleId, string reason)
    {
        if (!_isConnected || _channel == null) return;

        var @event = new ParticleExpiredEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            particleId,
            reason
        );

        await PublishEventAsync(@event, "particle.expired");
    }

    private async Task PublishEventAsync<T>(T @event, string routingKey) where T : class
    {
        try
        {
            var message = JsonSerializer.Serialize(@event);
            var body = Encoding.UTF8.GetBytes(message);

            var properties = _channel!.CreateBasicProperties();
            properties.ContentType = "application/json";
            properties.DeliveryMode = 2;
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            properties.Type = @event.GetType().Name;
            properties.MessageId = Guid.NewGuid().ToString();

            _channel.BasicPublish(
                exchange: _settings.ExchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body
            );

            _logger.LogDebug("Published {EventType} to {RoutingKey}", @event.GetType().Name, routingKey);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType}", typeof(T).Name);
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        _logger.LogInformation("SimulationEventPublisher disposed");
    }
}
