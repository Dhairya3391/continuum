using Scalar.AspNetCore;
using PersonalUniverse.SimulationEngine.API.Services;
using PersonalUniverse.Storage.API.Data;
using PersonalUniverse.Storage.API.Repositories;
using PersonalUniverse.Shared.Contracts.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

// Add background services
builder.Services.AddHostedService<PersonalUniverse.SimulationEngine.API.BackgroundServices.UniverseTickBackgroundService>();

// Add database connection factory
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddSingleton<IDbConnectionFactory>(new SqlConnectionFactory(connectionString));

// Register repositories
builder.Services.AddScoped<IParticleRepository, ParticleRepository>();
builder.Services.AddScoped<IPersonalityMetricsRepository, PersonalityMetricsRepository>();
builder.Services.AddScoped<IUniverseStateRepository, UniverseStateRepository>();

// Configure RabbitMQ event publishing
var rabbitMqSettings = new RabbitMqSettings
{
    Host = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost",
    Port = builder.Configuration.GetValue<int>("RabbitMQ:Port", 5672),
    Username = builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest",
    Password = builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest",
    VirtualHost = builder.Configuration.GetValue<string>("RabbitMQ:VirtualHost") ?? "/",
    ExchangeName = builder.Configuration.GetValue<string>("RabbitMQ:ExchangeName") ?? "personal_universe_events"
};
builder.Services.AddSingleton(rabbitMqSettings);
builder.Services.AddSingleton<ISimulationEventPublisher, SimulationEventPublisher>();

// Register services
builder.Services.AddScoped<IParticleService, ParticleService>();
builder.Services.AddScoped<ISimulationService, SimulationService>();
builder.Services.AddScoped<IInteractionService, InteractionService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
