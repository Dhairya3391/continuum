using Dapper;
using PersonalUniverse.Shared.Contracts.Interfaces;
using PersonalUniverse.Shared.Models.Entities;

namespace PersonalUniverse.SimulationEngine.API.Repositories;

public class ParticleRepository : IParticleRepository
{
    private readonly Data.IDbConnectionFactory _dbConnectionFactory;

    public ParticleRepository(Data.IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<Particle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM Particles WHERE Id = @Id",
            new { Id = id });
        
        return result == null ? null : MapToParticle(result);
    }

    public async Task<IEnumerable<Particle>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var results = await connection.QueryAsync<dynamic>("SELECT * FROM Particles");
        return results.Select(MapToParticle);
    }

    public async Task<Guid> AddAsync(Particle entity, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTime.UtcNow;
        entity.LastUpdatedAt = DateTime.UtcNow;
        
        await connection.ExecuteAsync(
            @"INSERT INTO Particles (Id, UserId, PositionX, PositionY, VelocityX, VelocityY, 
                Mass, Energy, State, CreatedAt, LastUpdatedAt, LastInputAt, DecayLevel) 
              VALUES (@Id, @UserId, @PositionX, @PositionY, @VelocityX, @VelocityY, 
                @Mass, @Energy, @State, @CreatedAt, @LastUpdatedAt, @LastInputAt, @DecayLevel)",
            new
            {
                entity.Id,
                entity.UserId,
                entity.PositionX,
                entity.PositionY,
                entity.VelocityX,
                entity.VelocityY,
                entity.Mass,
                entity.Energy,
                State = entity.State.ToString(),
                entity.CreatedAt,
                entity.LastUpdatedAt,
                entity.LastInputAt,
                entity.DecayLevel
            });
        
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(Particle entity, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        entity.LastUpdatedAt = DateTime.UtcNow;
        
        var rowsAffected = await connection.ExecuteAsync(
            @"UPDATE Particles 
              SET PositionX = @PositionX, PositionY = @PositionY, VelocityX = @VelocityX, VelocityY = @VelocityY,
                  Mass = @Mass, Energy = @Energy, State = @State, LastUpdatedAt = @LastUpdatedAt,
                  LastInputAt = @LastInputAt, DecayLevel = @DecayLevel
              WHERE Id = @Id",
            new
            {
                entity.Id,
                entity.PositionX,
                entity.PositionY,
                entity.VelocityX,
                entity.VelocityY,
                entity.Mass,
                entity.Energy,
                State = entity.State.ToString(),
                entity.LastUpdatedAt,
                entity.LastInputAt,
                entity.DecayLevel
            });
        
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(
            "DELETE FROM Particles WHERE Id = @Id",
            new { Id = id });
        
        return rowsAffected > 0;
    }

    public async Task<Particle?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM Particles WHERE UserId = @UserId",
            new { UserId = userId });
        
        return result == null ? null : MapToParticle(result);
    }

    public async Task<IEnumerable<Particle>> GetActiveParticlesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var results = await connection.QueryAsync<dynamic>(
            "SELECT * FROM Particles WHERE State = 'Active'");
        
        return results.Select(MapToParticle);
    }

    public async Task<IEnumerable<Particle>> GetParticlesInRegionAsync(
        double minX, double maxX, double minY, double maxY, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var results = await connection.QueryAsync<dynamic>(
            @"SELECT * FROM Particles 
              WHERE PositionX BETWEEN @MinX AND @MaxX 
                AND PositionY BETWEEN @MinY AND @MaxY",
            new { MinX = minX, MaxX = maxX, MinY = minY, MaxY = maxY });
        
        return results.Select(MapToParticle);
    }

    private static Particle MapToParticle(dynamic row)
    {
        return new Particle
        {
            Id = row.Id,
            UserId = row.UserId,
            PositionX = row.PositionX,
            PositionY = row.PositionY,
            VelocityX = row.VelocityX,
            VelocityY = row.VelocityY,
            Mass = row.Mass,
            Energy = row.Energy,
            State = Enum.Parse<ParticleState>(row.State),
            CreatedAt = row.CreatedAt,
            LastUpdatedAt = row.LastUpdatedAt,
            LastInputAt = row.LastInputAt,
            DecayLevel = row.DecayLevel
        };
    }
}
