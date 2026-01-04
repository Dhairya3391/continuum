using PersonalUniverse.EventService.API.Services;
using PersonalUniverse.EventService.API.BackgroundServices;
using PersonalUniverse.Shared.Contracts.Events;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Configure RabbitMQ settings
var rabbitMqSettings = new RabbitMqSettings
{
    HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") 
        ?? builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost",
    Port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var port) ? port 
        : builder.Configuration.GetValue<int>("RabbitMQ:Port", 5672),
    UserName = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") 
        ?? builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest",
    Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") 
        ?? builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest",
    VirtualHost = Environment.GetEnvironmentVariable("RABBITMQ_VHOST") 
        ?? builder.Configuration.GetValue<string>("RabbitMQ:VirtualHost") ?? "/",
    ExchangeName = Environment.GetEnvironmentVariable("RABBITMQ_EXCHANGE") 
        ?? builder.Configuration.GetValue<string>("RabbitMQ:ExchangeName") ?? "personaluniverse.events",
    ExchangeType = builder.Configuration.GetValue<string>("RabbitMQ:ExchangeType") ?? "topic"
};

builder.Services.AddSingleton(rabbitMqSettings);
builder.Services.AddSingleton<IEventPublisher, EventPublisher>();
builder.Services.AddSingleton<IEventSubscriber, EventSubscriber>();

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
