using Scalar.AspNetCore;
using PersonalUniverse.SimulationEngine.API.Services;
using PersonalUniverse.SimulationEngine.API.Jobs;
using PersonalUniverse.Storage.API.Data;
using PersonalUniverse.Storage.API.Repositories;
using PersonalUniverse.Shared.Contracts.Interfaces;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using StackExchange.Redis;

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
builder.Services.AddScoped<SimulationJobs>();

// Configure Redis
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
var redis = ConnectionMultiplexer.Connect(redisConnectionString);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddSingleton<ICacheService, RedisCacheService>();

// Add Hangfire
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString, new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

builder.Services.AddHangfireServer();

// Configure JWT Authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "PersonalUniverseSimulator";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "PersonalUniverseClients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))
        };
    });

builder.Services.AddAuthorization();

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
