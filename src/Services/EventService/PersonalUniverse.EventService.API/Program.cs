using PersonalUniverse.EventService.API.Services;
using PersonalUniverse.EventService.API.BackgroundServices;
using PersonalUniverse.Shared.Contracts.Events;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Configure RabbitMQ settings
var rabbitHostFromEnv = Environment.GetEnvironmentVariable("RABBITMQ_HOST");
var rabbitHostFromConfig = builder.Configuration.GetValue<string>("RabbitMQ:Host");
var rabbitPortFromEnv = Environment.GetEnvironmentVariable("RABBITMQ_PORT");
var parsedPort = int.TryParse(rabbitPortFromEnv, out var rabbitPortEnvValue) ? rabbitPortEnvValue : (int?)null;
var rabbitPortFromConfig = builder.Configuration.GetValue<int>("RabbitMQ:Port", 5672);
var rabbitPort = parsedPort ?? rabbitPortFromConfig;

var useSsl = bool.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_USE_SSL"), out var envUseSsl)
    ? envUseSsl
    : builder.Configuration.GetValue<bool?>("RabbitMQ:UseSsl")
        ?? (rabbitPort == 5671);

var allowNoOpFallback = bool.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_ALLOW_NOOP"), out var allowNoOpEnv)
    ? allowNoOpEnv
    : builder.Configuration.GetValue<bool?>("RabbitMQ:AllowNoOpFallback")
        ?? rabbitHostFromEnv is null;

var rabbitMqSettings = new RabbitMqSettings
{
    HostName = rabbitHostFromEnv ?? rabbitHostFromConfig ?? "localhost",
    Port = rabbitPort,
    UserName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") 
        ?? builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest",
    Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") 
        ?? builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest",
    VirtualHost = Environment.GetEnvironmentVariable("RABBITMQ_VHOST") 
        ?? builder.Configuration.GetValue<string>("RabbitMQ:VirtualHost") ?? "/",
    ExchangeName = Environment.GetEnvironmentVariable("RABBITMQ_EXCHANGE") 
        ?? builder.Configuration.GetValue<string>("RabbitMQ:ExchangeName") ?? "personaluniverse.events",
    ExchangeType = builder.Configuration.GetValue<string>("RabbitMQ:ExchangeType") ?? "topic",
    UseSsl = useSsl
};

builder.Services.AddSingleton(rabbitMqSettings);
builder.Services.AddSingleton<IEventPublisher>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<EventPublisher>>();
    try
    {
        return new EventPublisher(rabbitMqSettings, logger);
    }
    catch (Exception ex)
    {
        if (!allowNoOpFallback)
        {
            logger.LogCritical(ex, "RabbitMQ connection failed and no-op fallback is disabled. Check RABBITMQ_* values and reachability.");
            throw;
        }

        logger.LogWarning(ex, "RabbitMQ unavailable. Using no-op publisher.");
        var noopLogger = sp.GetRequiredService<ILogger<NoOpEventPublisher>>();
        return new NoOpEventPublisher(noopLogger);
    }
});

builder.Services.AddSingleton<IEventSubscriber>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<EventSubscriber>>();
    try
    {
        return new EventSubscriber(rabbitMqSettings, logger);
    }
    catch (Exception ex)
    {
        if (!allowNoOpFallback)
        {
            logger.LogCritical(ex, "RabbitMQ connection failed and no-op fallback is disabled. Check RABBITMQ_* values and reachability.");
            throw;
        }

        logger.LogWarning(ex, "RabbitMQ unavailable. Using no-op subscriber.");
        var noopLogger = sp.GetRequiredService<ILogger<NoOpEventSubscriber>>();
        return new NoOpEventSubscriber(noopLogger);
    }
});

// Add background service for event consumption
builder.Services.AddHostedService<EventConsumerBackgroundService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors();
app.MapControllers();

app.Run();
