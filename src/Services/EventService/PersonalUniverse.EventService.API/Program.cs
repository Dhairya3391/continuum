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
    HostName = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost",
    Port = builder.Configuration.GetValue<int>("RabbitMQ:Port", 5672),
    UserName = builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest",
    Password = builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest",
    VirtualHost = builder.Configuration.GetValue<string>("RabbitMQ:VirtualHost") ?? "/",
    ExchangeName = builder.Configuration.GetValue<string>("RabbitMQ:ExchangeName") ?? "personaluniverse.events",
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
