using Dapper;
using IotIngest.Api.Domain;
using Microsoft.Data.SqlClient;

namespace IotIngest.Api.Data.Repositories;

public interface IAuthRepository
{
    Task<AuthUser?> GetUserByUsernameAsync(string username);
    Task<(AuthUser User, IEnumerable<AuthRole> Roles)> GetUserWithRolesAsync(string username);
    Task<int[]> GetUserDeviceScopesAsync(int userId);
    Task UpdateLastLoginAsync(int userId);
    Task<long> CreateRefreshTokenAsync(int userId, string tokenHash, DateTime expiresUtc);
    Task<AuthRefreshToken?> GetRefreshTokenAsync(string tokenHash);
    Task RevokeRefreshTokenAsync(long tokenId, string? replacedByHash = null);
    Task RevokeUserTokensAsync(int userId);
    
    // User Management
    Task<IEnumerable<UserDto>> GetAllUsersAsync();
    Task<UserDto?> GetUserByIdAsync(int userId);
    Task<IEnumerable<RoleDto>> GetAllRolesAsync();
    Task<int> CreateUserAsync(CreateUserDto dto, string passwordHash);
    Task UpdateUserAsync(int userId, UpdateUserDto dto, string? passwordHash = null);
    Task DeleteUserAsync(int userId);
    Task SetUserRolesAsync(int userId, int[] roleIds);
    Task SetUserDeviceScopesAsync(int userId, int[] deviceIds);
}

public class AuthRepository : IAuthRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuthRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AuthUser?> GetUserByUsernameAsync(string username)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<AuthUser>(Sql.Auth.GetUserByUsername, new { Username = username });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving user by username: {username}", ex);
        }
    }

    public async Task<(AuthUser User, IEnumerable<AuthRole> Roles)> GetUserWithRolesAsync(string username)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var lookup = new Dictionary<int, AuthUser>();
        var roles = new List<AuthRole>();

        var result = await connection.QueryAsync<AuthUser, AuthRole, AuthUser>(
            Sql.Auth.GetUserWithRoles,
            (user, role) =>
            {
                if (!lookup.TryGetValue(user.Id, out var existingUser))
                {
                    lookup.Add(user.Id, user);
                    existingUser = user;
                }

                roles.Add(role);
                return existingUser;
            },
            new { Username = username },
            splitOn: "RoleId");

        var user = lookup.Values.FirstOrDefault();
        return user != null ? (user, roles) : (null!, Enumerable.Empty<AuthRole>());
    }

    public async Task<int[]> GetUserDeviceScopesAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var deviceIds = await connection.QueryAsync<int>(Sql.Auth.GetUserDeviceScopes, new { UserId = userId });
        return deviceIds.ToArray();
    }

    public async Task UpdateLastLoginAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(Sql.Auth.UpdateLastLogin, new { UserId = userId });
    }

    public async Task<long> CreateRefreshTokenAsync(int userId, string tokenHash, DateTime expiresUtc)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var sql = Sql.Auth.CreateRefreshToken + "; SELECT SCOPE_IDENTITY();";
        var id = await connection.QuerySingleAsync<long>(sql, new { UserId = userId, TokenHash = tokenHash, ExpiresUtc = expiresUtc });
        return id;
    }

    public async Task<AuthRefreshToken?> GetRefreshTokenAsync(string tokenHash)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<AuthRefreshToken>(Sql.Auth.GetRefreshToken, new { TokenHash = tokenHash });
    }

    public async Task RevokeRefreshTokenAsync(long tokenId, string? replacedByHash = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(Sql.Auth.RevokeRefreshToken, new { Id = tokenId, ReplacedByHash = replacedByHash });
    }

    public async Task RevokeUserTokensAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(Sql.Auth.RevokeUserTokens, new { UserId = userId });
    }

    // User Management Implementation
    public async Task<IEnumerable<UserDto>> GetAllUsersAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var users = await connection.QueryAsync<AuthUser>(Sql.Auth.GetAllUsers);
        
        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await connection.QueryAsync<AuthRole>(Sql.Auth.GetUserRoles, new { UserId = user.Id });
            var deviceIds = await connection.QueryAsync<int>(Sql.Auth.GetUserDeviceScopes, new { UserId = user.Id });
            
            result.Add(new UserDto(
                user.Id,
                user.Username,
                user.DisplayName,
                user.IsActive,
                user.LastLoginUtc,
                user.CreatedUtc,
                roles.Select(r => r.Name).ToArray(),
                deviceIds.ToArray()));
        }
        return result;
    }

    public async Task<UserDto?> GetUserByIdAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var user = await connection.QueryFirstOrDefaultAsync<AuthUser>(Sql.Auth.GetUserById, new { UserId = userId });
        
        if (user == null) return null;
        
        var roles = await connection.QueryAsync<AuthRole>(Sql.Auth.GetUserRoles, new { UserId = user.Id });
        var deviceIds = await connection.QueryAsync<int>(Sql.Auth.GetUserDeviceScopes, new { UserId = user.Id });
        
        return new UserDto(
            user.Id,
            user.Username,
            user.DisplayName,
            user.IsActive,
            user.LastLoginUtc,
            user.CreatedUtc,
            roles.Select(r => r.Name).ToArray(),
            deviceIds.ToArray());
    }

    public async Task<IEnumerable<RoleDto>> GetAllRolesAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        var roles = await connection.QueryAsync<AuthRole>(Sql.Auth.GetAllRoles);
        return roles.Select(r => new RoleDto(r.Id, r.Name, r.Level));
    }

    public async Task<int> CreateUserAsync(CreateUserDto dto, string passwordHash)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var userId = await connection.QuerySingleAsync<int>(Sql.Auth.CreateUser, new 
            { 
                dto.Username, 
                dto.DisplayName, 
                PasswordHash = passwordHash, 
                dto.IsActive 
            });
            
            return userId;
        }
        catch (SqlException sqlEx) when (sqlEx.Number == 2627) // UNIQUE constraint violation
        {
            throw new InvalidOperationException($"Username '{dto.Username}' already exists. Please choose a different username.", sqlEx);
        }
        catch (SqlException sqlEx) when (sqlEx.Number == 2) // Cannot open database
        {
            throw new InvalidOperationException("Database connection failed. Please try again later.", sqlEx);
        }
        catch (SqlException sqlEx) when (sqlEx.Number == -2) // Timeout
        {
            throw new InvalidOperationException("Database operation timed out. Please try again.", sqlEx);
        }
        catch (SqlException sqlEx)
        {
            throw new InvalidOperationException($"Database error while creating user '{dto.Username}': {sqlEx.Message}", sqlEx);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unexpected error creating user: {dto.Username}", ex);
        }
    }

    public async Task UpdateUserAsync(int userId, UpdateUserDto dto, string? passwordHash = null)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        if (passwordHash != null)
        {
            await connection.ExecuteAsync(Sql.Auth.UpdateUserPassword, new 
            { 
                Id = userId, 
                dto.DisplayName, 
                dto.IsActive, 
                PasswordHash = passwordHash 
            });
        }
        else
        {
            await connection.ExecuteAsync(Sql.Auth.UpdateUser, new 
            { 
                Id = userId, 
                dto.DisplayName, 
                dto.IsActive 
            });
        }
    }

    public async Task DeleteUserAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(Sql.Auth.DeleteUser, new { UserId = userId });
    }

    public async Task SetUserRolesAsync(int userId, int[] roleIds)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                await connection.ExecuteAsync(Sql.Auth.DeleteUserRoles, new { UserId = userId }, transaction);
                
                foreach (var roleId in roleIds)
                {
                    await connection.ExecuteAsync(Sql.Auth.InsertUserRole, new { UserId = userId, RoleId = roleId }, transaction);
                }
                
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error setting user roles for user ID: {userId}", ex);
        }
    }

    public async Task SetUserDeviceScopesAsync(int userId, int[] deviceIds)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                await connection.ExecuteAsync(Sql.Auth.DeleteUserDeviceScopes, new { UserId = userId }, transaction);
                
                foreach (var deviceId in deviceIds)
                {
                    await connection.ExecuteAsync(Sql.Auth.InsertUserDeviceScope, new { UserId = userId, DeviceId = deviceId }, transaction);
                }
                
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error setting user device scopes for user ID: {userId}", ex);
        }
    }
}