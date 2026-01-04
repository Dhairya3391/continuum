using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PersonalUniverse.Identity.API;
using Xunit;

namespace PersonalUniverse.IntegrationTests.Infrastructure;

public class IntegrationTestFactory : WebApplicationFactory<IApiMarker>, IAsyncLifetime
{
    private readonly string _sqlConnectionString;
    private readonly string _redisConnectionString;
    private readonly string _rabbitHost;
    private readonly string _rabbitPort;
    private readonly string _rabbitUsername;
    private readonly string _rabbitPassword;
    private readonly string _rabbitVhost;
    private readonly string _rabbitExchange;

    public IntegrationTestFactory()
    {
        LoadDotEnv();

        _sqlConnectionString = RequireEnv("DB_CONNECTION_STRING");
        _redisConnectionString = RequireEnv("REDIS_CONNECTION_STRING");
        _rabbitHost = RequireEnv("RABBITMQ_HOST");

        _rabbitPort = Environment.GetEnvironmentVariable("RABBITMQ_PORT") ?? "5672";
        _rabbitUsername = Environment.GetEnvironmentVariable("RABBITMQ_USERNAME") ?? "guest";
        _rabbitPassword = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest";
        _rabbitVhost = Environment.GetEnvironmentVariable("RABBITMQ_VHOST") ?? "/";
        _rabbitExchange = Environment.GetEnvironmentVariable("RABBITMQ_EXCHANGE") ?? "personaluniverse.events";
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new Task DisposeAsync() => Task.CompletedTask;

    private static string RequireEnv(string key)
    {
        var value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Integration tests require {key} to be set (no Docker). Load .env or export the variable.");
        }

        return value;
    }

    private static void LoadDotEnv()
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && current is not null; i++)
        {
            var candidate = Path.Combine(current, ".env");
            if (File.Exists(candidate))
            {
                foreach (var line in File.ReadAllLines(candidate))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")) continue;
                    var idx = line.IndexOf('=');
                    if (idx <= 0) continue;
                    var key = line[..idx].Trim();
                    var val = line[(idx + 1)..].Trim();
                    if (!string.IsNullOrEmpty(key))
                    {
                        Environment.SetEnvironmentVariable(key, val);
                    }
                }
                return;
            }

            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Override connection strings with test containers
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _sqlConnectionString,
                    ["ConnectionStrings:Redis"] = _redisConnectionString,
                    ["RabbitMQ:Host"] = _rabbitHost,
                    ["RabbitMQ:Port"] = _rabbitPort,
                    ["RabbitMQ:Username"] = _rabbitUsername,
                    ["RabbitMQ:Password"] = _rabbitPassword,
                    ["RabbitMQ:VirtualHost"] = _rabbitVhost,
                    ["RabbitMQ:ExchangeName"] = _rabbitExchange,
                    ["Jwt:SecretKey"] = "TestSecretKeyForIntegrationTests123!@#",
                    ["Jwt:Issuer"] = "PersonalUniverseSimulator",
                    ["Jwt:Audience"] = "PersonalUniverseClients"
                });
            });
        });

        return base.CreateHost(builder);
    }
}
