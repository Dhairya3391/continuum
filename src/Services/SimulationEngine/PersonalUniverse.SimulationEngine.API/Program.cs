using Scalar.AspNetCore;
using PersonalUniverse.SimulationEngine.API.Services;
using PersonalUniverse.SimulationEngine.API.Jobs;
using PersonalUniverse.SimulationEngine.API.Data;
using PersonalUniverse.SimulationEngine.API.Repositories;
using PersonalUniverse.Shared.Contracts.Interfaces;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using StackExchange.Redis;
using PersonalUniverse.SimulationEngine.API;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

// Add background services
builder.Services.AddHostedService<PersonalUniverse.SimulationEngine.API.BackgroundServices.UniverseTickBackgroundService>();

// Add database connection factory
var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING") 
    ?? builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string not found in environment or configuration.");
builder.Services.AddSingleton<IDbConnectionFactory>(new SqlConnectionFactory(connectionString));

// Register repositories
builder.Services.AddScoped<IParticleRepository, ParticleRepository>();
builder.Services.AddScoped<IPersonalityMetricsRepository, PersonalityMetricsRepository>();
builder.Services.AddScoped<IUniverseStateRepository, UniverseStateRepository>();

// Configure RabbitMQ event publishing
var rabbitMqSettings = new RabbitMqSettings
{
    Host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") 
        ?? builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost",
    Port = int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out var port) ? port 
        : builder.Configuration.GetValue<int>("RabbitMQ:Port", 5672),
    Username = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") 
        ?? builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "guest",
    Password = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") 
        ?? builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "guest",
    VirtualHost = Environment.GetEnvironmentVariable("RABBITMQ_VHOST") 
        ?? builder.Configuration.GetValue<string>("RabbitMQ:VirtualHost") ?? "/",
    ExchangeName = Environment.GetEnvironmentVariable("RABBITMQ_EXCHANGE") 
        ?? builder.Configuration.GetValue<string>("RabbitMQ:ExchangeName") ?? "personaluniverse.events"
};
builder.Services.AddSingleton(rabbitMqSettings);
builder.Services.AddSingleton<ISimulationEventPublisher, SimulationEventPublisher>();

// Register services
builder.Services.AddScoped<IParticleService, ParticleService>();
builder.Services.AddScoped<ISimulationService, SimulationService>();
builder.Services.AddScoped<IInteractionService, InteractionService>();
builder.Services.AddScoped<SimulationJobs>();

// Configure Redis
var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING") 
    ?? builder.Configuration.GetConnectionString("Redis") 
    ?? "localhost:6379";
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
var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
    ?? builder.Configuration["Jwt:SecretKey"]
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

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1)
            });
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

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
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Add Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

// Schedule recurring jobs
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    
    // Daily simulation tick at midnight UTC
    recurringJobManager.AddOrUpdate<SimulationJobs>(
        "daily-universe-tick",
        job => job.ProcessDailyTickAsync(),
        "0 0 * * *", // Every day at midnight
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc
        });
    
    // Decay processing every 6 hours
    recurringJobManager.AddOrUpdate<SimulationJobs>(
        "particle-decay-check",
        job => job.ProcessParticleDecayAsync(),
        "0 */6 * * *", // Every 6 hours
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.Utc
        });
}

app.MapControllers();

app.Run();
