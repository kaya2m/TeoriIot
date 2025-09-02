using Dapper;
using IotIngest.Api.Domain;

namespace IotIngest.Api.Data.Repositories;

public interface IDeviceRepository
{
    Task<Device?> GetByNodeNumAsync(int nodeNum);
    Task<Device?> GetActiveByNodeNumAsync(int nodeNum);
    Task<IEnumerable<DeviceDto>> GetAllAsync();
    Task<IEnumerable<DeviceDto>> GetForUserAsync(int userId);
    Task UpdateLastSeenAsync(int deviceId, string? ipAddress);
    Task CreateAsync(CreateDeviceDto dto);
    Task UpdateAsync(int id, CreateDeviceDto dto);
}

public class DeviceRepository : IDeviceRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DeviceRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Device?> GetByNodeNumAsync(int nodeNum)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<Device>(Sql.Devices.GetByNodeNum, new { NodeNum = nodeNum });
    }

    public async Task<Device?> GetActiveByNodeNumAsync(int nodeNum)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<Device>(Sql.Devices.GetActiveByNodeNum, new { NodeNum = nodeNum });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving active device by node number: {nodeNum}", ex);
        }
    }

    public async Task<IEnumerable<DeviceDto>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<DeviceDto>(Sql.Devices.GetAll);
    }

    public async Task<IEnumerable<DeviceDto>> GetForUserAsync(int userId)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryAsync<DeviceDto>(Sql.Devices.GetForUser, new { UserId = userId });
    }

    public async Task UpdateLastSeenAsync(int deviceId, string? ipAddress)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            await connection.ExecuteAsync(Sql.Devices.UpdateLastSeen, new { DeviceId = deviceId, IpAddress = ipAddress });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error updating last seen for device ID: {deviceId}", ex);
        }
    }

    public async Task CreateAsync(CreateDeviceDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(Sql.Devices.Create, dto);
    }

    public async Task UpdateAsync(int id, CreateDeviceDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(Sql.Devices.Update, new { Id = id, dto.Name, dto.ApiKey, dto.IsActive });
    }
}