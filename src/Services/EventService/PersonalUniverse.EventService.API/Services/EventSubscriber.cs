using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using PersonalUniverse.Shared.Contracts.Events;
using System.Text;
using System.Text.Json;

namespace PersonalUniverse.EventService.API.Services;

public class EventSubscriber : IEventSubscriber, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<EventSubscriber> _logger;
    private readonly Dictionary<string, List<Func<string, Task>>> _handlers = new();

    public EventSubscriber(RabbitMqSettings settings, ILogger<EventSubscriber> logger)
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

            _logger.LogInformation("EventSubscriber initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize EventSubscriber");
            throw;
        }
    }

    public async Task SubscribeAsync(string routingKey, Func<string, Task> handler)
    {
        try
        {
            // Create unique queue name for this subscription
            var queueName = $"queue.{routingKey}.{Guid.NewGuid()}";

            // Declare queue
            _channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: true
            );

            // Bind queue to exchange with routing key
            _channel.QueueBind(
                queue: queueName,
                exchange: _settings.ExchangeName,
                routingKey: routingKey
            );

            // Create consumer
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        _logger.LogInformation(
                            "Received event on routing key {RoutingKey}: {Message}",
                            ea.RoutingKey,
                            message
                        );

                        await handler(message);

                        // Acknowledge message
                        _channel.BasicAck(ea.DeliveryTag, false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message");
                        // Reject and requeue on error
                        _channel.BasicNack(ea.DeliveryTag, false, true);
                    }
                });
            };

            _channel.BasicConsume(
                queue: queueName,
                autoAck: false,
                consumer: consumer
            );

            // Store handler reference
            if (!_handlers.ContainsKey(routingKey))
            {
                _handlers[routingKey] = new List<Func<string, Task>>();
            }
            _handlers[routingKey].Add(handler);

            _logger.LogInformation("Subscribed to routing key: {RoutingKey}", routingKey);
            
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to routing key {RoutingKey}", routingKey);
            throw;
        }
    }

    public async Task SubscribeAsync<T>(string routingKey, Func<T, Task> handler) where T : class
    {
        await SubscribeAsync(routingKey, async (message) =>
        {
            try
            {
                var @event = JsonSerializer.Deserialize<T>(message);
                if (@event != null)
                {
                    await handler(@event);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize event of type {EventType}", typeof(T).Name);
            }
        });
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        _logger.LogInformation("EventSubscriber disposed");
    }
}
