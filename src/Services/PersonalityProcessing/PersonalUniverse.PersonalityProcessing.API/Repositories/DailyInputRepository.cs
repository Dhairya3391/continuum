using Dapper;
using PersonalUniverse.Shared.Contracts.Interfaces;
using PersonalUniverse.Shared.Models.Entities;

namespace PersonalUniverse.PersonalityProcessing.API.Repositories;

public class DailyInputRepository : IDailyInputRepository
{
    private readonly Data.IDbConnectionFactory _dbConnectionFactory;

    public DailyInputRepository(Data.IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<DailyInput?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var result = await connection.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM DailyInputs WHERE Id = @Id",
            new { Id = id });
        
        return result == null ? null : MapToDailyInput(result);
    }

    public async Task<IEnumerable<DailyInput>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var results = await connection.QueryAsync<dynamic>("SELECT * FROM DailyInputs");
        return results.Select(MapToDailyInput);
    }

    public async Task<Guid> AddAsync(DailyInput entity, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        entity.Id = Guid.NewGuid();
        entity.SubmittedAt = DateTime.UtcNow;

        await connection.ExecuteAsync(
            @"INSERT INTO DailyInputs (Id, UserId, ParticleId, Type, Question, Response, 
                NumericValue, SubmittedAt, Processed) 
              VALUES (@Id, @UserId, @ParticleId, @Type, @Question, @Response, 
                @NumericValue, @SubmittedAt, @Processed)",
            new
            {
                entity.Id,
                entity.UserId,
                entity.ParticleId,
                Type = entity.Type.ToString(),
                entity.Question,
                entity.Response,
                entity.NumericValue,
                entity.SubmittedAt,
                entity.Processed
            });

        return entity.Id;
    }

    public async Task<bool> UpdateAsync(DailyInput entity, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(
            @"UPDATE DailyInputs 
              SET Processed = @Processed
              WHERE Id = @Id",
            new { entity.Id, entity.Processed });

        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(
            "DELETE FROM DailyInputs WHERE Id = @Id",
            new { Id = id });

        return rowsAffected > 0;
    }

    public async Task<IEnumerable<DailyInput>> GetUnprocessedInputsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var results = await connection.QueryAsync<dynamic>(
            "SELECT * FROM DailyInputs WHERE Processed = 0 ORDER BY SubmittedAt");
        
        return results.Select(MapToDailyInput);
    }

    public async Task<int> GetDailyInputCountAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        return await connection.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM DailyInputs 
              WHERE UserId = @UserId 
                AND SubmittedAt >= @StartOfDay 
                AND SubmittedAt < @EndOfDay",
            new { UserId = userId, StartOfDay = startOfDay, EndOfDay = endOfDay });
    }

    private static DailyInput MapToDailyInput(dynamic row)
    {
        return new DailyInput
        {
            Id = row.Id,
            UserId = row.UserId,
            ParticleId = row.ParticleId,
            Type = Enum.Parse<InputType>(row.Type),
            Question = row.Question,
            Response = row.Response,
            NumericValue = row.NumericValue,
            SubmittedAt = row.SubmittedAt,
            Processed = row.Processed
        };
    }
}
