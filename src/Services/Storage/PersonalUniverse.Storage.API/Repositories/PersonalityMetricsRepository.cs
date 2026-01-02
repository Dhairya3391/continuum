using Dapper;
using PersonalUniverse.Shared.Contracts.Interfaces;
using PersonalUniverse.Shared.Models.Entities;

namespace PersonalUniverse.Storage.API.Repositories;

public class PersonalityMetricsRepository : IPersonalityMetricsRepository
{
    private readonly Data.IDbConnectionFactory _dbConnectionFactory;

    public PersonalityMetricsRepository(Data.IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<PersonalityMetrics?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<PersonalityMetrics>(
            "SELECT * FROM PersonalityMetrics WHERE Id = @Id",
            new { Id = id });
    }

    public async Task<IEnumerable<PersonalityMetrics>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.QueryAsync<PersonalityMetrics>("SELECT * FROM PersonalityMetrics");
    }

    public async Task<Guid> AddAsync(PersonalityMetrics entity, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        entity.Id = Guid.NewGuid();
        entity.CalculatedAt = DateTime.UtcNow;

        await connection.ExecuteAsync(
            @"INSERT INTO PersonalityMetrics (Id, ParticleId, Curiosity, SocialAffinity, Aggression, 
                Stability, GrowthPotential, CalculatedAt, Version) 
              VALUES (@Id, @ParticleId, @Curiosity, @SocialAffinity, @Aggression, 
                @Stability, @GrowthPotential, @CalculatedAt, @Version)",
            entity);

        return entity.Id;
    }

    public async Task<bool> UpdateAsync(PersonalityMetrics entity, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(
            @"UPDATE PersonalityMetrics 
              SET Curiosity = @Curiosity, SocialAffinity = @SocialAffinity, Aggression = @Aggression,
                  Stability = @Stability, GrowthPotential = @GrowthPotential, 
                  CalculatedAt = @CalculatedAt, Version = @Version
              WHERE Id = @Id",
            entity);

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(
            "DELETE FROM PersonalityMetrics WHERE Id = @Id",
            new { Id = id });

        return rowsAffected > 0;
    }

    public async Task<PersonalityMetrics?> GetLatestByParticleIdAsync(Guid particleId, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<PersonalityMetrics>(
            @"SELECT TOP 1 * FROM PersonalityMetrics 
              WHERE ParticleId = @ParticleId 
              ORDER BY CalculatedAt DESC",
            new { ParticleId = particleId });
    }

    public async Task<IEnumerable<PersonalityMetrics>> GetHistoryByParticleIdAsync(Guid particleId, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.QueryAsync<PersonalityMetrics>(
            @"SELECT * FROM PersonalityMetrics 
              WHERE ParticleId = @ParticleId 
              ORDER BY CalculatedAt DESC",
            new { ParticleId = particleId });
    }
}
