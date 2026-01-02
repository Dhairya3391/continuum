using RabbitMQ.Client;
using PersonalUniverse.Shared.Contracts.Events;
using System.Text;
using System.Text.Json;

namespace PersonalUniverse.EventService.API.Services;

public class EventPublisher : IEventPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(RabbitMqSettings settings, ILogger<EventPublisher> logger)
    {
        _settings = settings;
        _logger = logger;

        var factory = new ConnectionFactory
        {
            HostName = settings.HostName,
            Port = settings.Port,
            UserName = settings.UserName,
            Password = settings.Password,
            VirtualHost = settings.VirtualHost
        };

        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare exchange
            _channel.ExchangeDeclare(
                exchange: settings.ExchangeName,
                type: settings.ExchangeType,
                durable: true,
                autoDelete: false
            );

            _logger.LogInformation("EventPublisher initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize EventPublisher");
            throw;
        }
    }

    public async Task PublishAsync<T>(T @event, string routingKey) where T : class
    {
        try
        {
            var eventType = @event.GetType().Name;
            var message = JsonSerializer.Serialize(@event);
            var body = Encoding.UTF8.GetBytes(message);

            var properties = _channel.CreateBasicProperties();
            properties.ContentType = "application/json";
            properties.DeliveryMode = 2; // Persistent
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            properties.Type = eventType;
            properties.MessageId = Guid.NewGuid().ToString();

            _channel.BasicPublish(
                exchange: _settings.ExchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body
            );

            _logger.LogInformation(
                "Published event {EventType} with routing key {RoutingKey}",
                eventType,
                routingKey
            );
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event of type {EventType}", typeof(T).Name);
            throw;
        }
    }

    public async Task PublishParticleSpawnedAsync(ParticleSpawnedEvent @event)
    {
        await PublishAsync(@event, "particle.spawned");
    }

    public async Task PublishParticleMergedAsync(ParticleMergedEvent @event)
    {
        await PublishAsync(@event, "particle.merged");
    }

    public async Task PublishParticleRepelledAsync(ParticleRepelledEvent @event)
    {
        await PublishAsync(@event, "particle.repelled");
    }

    public async Task PublishParticleSplitAsync(ParticleSplitEvent @event)
    {
        await PublishAsync(@event, "particle.split");
    }

    public async Task PublishParticleExpiredAsync(ParticleExpiredEvent @event)
    {
        await PublishAsync(@event, "particle.expired");
    }

    public async Task PublishDailyProcessingCompletedAsync(DailyProcessingCompletedEvent @event)
    {
        await PublishAsync(@event, "universe.daily.completed");
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        _logger.LogInformation("EventPublisher disposed");
    }
}
