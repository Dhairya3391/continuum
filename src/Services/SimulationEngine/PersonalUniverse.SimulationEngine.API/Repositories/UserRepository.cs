using Dapper;
using PersonalUniverse.Shared.Contracts.Interfaces;
using PersonalUniverse.Shared.Models.Entities;

namespace PersonalUniverse.SimulationEngine.API.Repositories;

public class UserRepository : IUserRepository
{
    private readonly Data.IDbConnectionFactory _dbConnectionFactory;

    public UserRepository(Data.IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Id = @Id",
            new { Id = id });
    }

    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.QueryAsync<User>("SELECT * FROM Users");
    }

    public async Task<Guid> AddAsync(User entity, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTime.UtcNow;
        
        await connection.ExecuteAsync(
            @"INSERT INTO Users (Id, Username, Email, PasswordHash, CreatedAt, LastLoginAt, IsActive, AuthProvider, ExternalId, ProfilePictureUrl) 
              VALUES (@Id, @Username, @Email, @PasswordHash, @CreatedAt, @LastLoginAt, @IsActive, @AuthProvider, @ExternalId, @ProfilePictureUrl)",
            entity);
        
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(User entity, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(
            @"UPDATE Users 
              SET Username = @Username, Email = @Email, PasswordHash = @PasswordHash, 
                  LastLoginAt = @LastLoginAt, IsActive = @IsActive,
                  AuthProvider = @AuthProvider, ExternalId = @ExternalId, ProfilePictureUrl = @ProfilePictureUrl
              WHERE Id = @Id",
            entity);
        
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var rowsAffected = await connection.ExecuteAsync(
            "DELETE FROM Users WHERE Id = @Id",
            new { Id = id });
        
        return rowsAffected > 0;
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Email = @Email",
            new { Email = email });
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM Users WHERE Username = @Username",
            new { Username = username });
    }
}
