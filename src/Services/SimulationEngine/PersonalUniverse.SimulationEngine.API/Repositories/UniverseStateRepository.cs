using Dapper;
using PersonalUniverse.Shared.Contracts.Interfaces;
using PersonalUniverse.Shared.Models.Entities;

namespace PersonalUniverse.SimulationEngine.API.Repositories;

public class UniverseStateRepository : IUniverseStateRepository
{
    private readonly Data.IDbConnectionFactory _dbConnectionFactory;

    public UniverseStateRepository(Data.IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<UniverseState?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<UniverseState>(
            "SELECT * FROM UniverseStates WHERE Id = @Id",
            new { Id = id });
    }

    public async Task<IEnumerable<UniverseState>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.QueryAsync<UniverseState>(
            "SELECT * FROM UniverseStates ORDER BY TickNumber DESC");
    }

    public async Task<Guid> AddAsync(UniverseState entity, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        entity.Id = Guid.NewGuid();
        entity.Timestamp = DateTime.UtcNow;

        await connection.ExecuteAsync(
            @"INSERT INTO UniverseStates (Id, TickNumber, Timestamp, ActiveParticleCount, 
                AverageEnergy, InteractionCount, SnapshotData) 
              VALUES (@Id, @TickNumber, @Timestamp, @ActiveParticleCount, 
                @AverageEnergy, @InteractionCount, @SnapshotData)",
            entity);

        return entity.Id;
    }

    public async Task<bool> UpdateAsync(UniverseState entity, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(
            @"UPDATE UniverseStates 
              SET ActiveParticleCount = @ActiveParticleCount, AverageEnergy = @AverageEnergy,
                  InteractionCount = @InteractionCount, SnapshotData = @SnapshotData
              WHERE Id = @Id",
            entity);

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(
            "DELETE FROM UniverseStates WHERE Id = @Id",
            new { Id = id });

        return rowsAffected > 0;
    }

    public async Task<UniverseState?> GetLatestStateAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<UniverseState>(
            "SELECT TOP 1 * FROM UniverseStates ORDER BY TickNumber DESC");
    }

    public async Task<UniverseState?> GetStateByTickAsync(int tickNumber, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<UniverseState>(
            "SELECT * FROM UniverseStates WHERE TickNumber = @TickNumber",
            new { TickNumber = tickNumber });
    }
}
