using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

namespace PersonalUniverse.IntegrationTests.Infrastructure;

public class IntegrationTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("TestP@ssw0rd123!")
        .Build();

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder()
        .WithImage("rabbitmq:4-management")
        .Build();

    private readonly RedisContainer _redisContainer = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public string SqlConnectionString => _sqlContainer.GetConnectionString();
    public string RabbitMqConnectionString => _rabbitMqContainer.GetConnectionString();
    public string RedisConnectionString => _redisContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();
        await _rabbitMqContainer.StartAsync();
        await _redisContainer.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await _rabbitMqContainer.DisposeAsync();
        await _redisContainer.DisposeAsync();
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Override connection strings with test containers
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["ConnectionStrings:DefaultConnection"] = SqlConnectionString,
                    ["ConnectionStrings:Redis"] = RedisConnectionString,
                    ["RabbitMQ:Host"] = _rabbitMqContainer.Hostname,
                    ["RabbitMQ:Port"] = _rabbitMqContainer.GetMappedPublicPort(5672).ToString(),
                    ["RabbitMQ:Username"] = "guest",
                    ["RabbitMQ:Password"] = "guest",
                    ["Jwt:SecretKey"] = "TestSecretKeyForIntegrationTests123!@#",
                    ["Jwt:Issuer"] = "PersonalUniverseSimulator",
                    ["Jwt:Audience"] = "PersonalUniverseClients"
                });
            });
        });

        return base.CreateHost(builder);
    }
}
