using Dapper;
using IotIngest.Api.Domain;

namespace IotIngest.Api.Data.Repositories;

public interface IInputRepository
{
    Task<IEnumerable<DeviceInputDto>> GetByDeviceAsync(int deviceId);
    Task<Input?> GetByDevicePinAsync(int deviceId, byte pinIndex);
    Task UpsertAsync(int deviceId, byte pinIndex, bool lastState, DateTime updatedUtc);
    Task UpdateSettingsAsync(int deviceId, byte pinIndex, UpdateInputDto dto);
}

public class InputRepository : IInputRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public InputRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IEnumerable<DeviceInputDto>> GetByDeviceAsync(int deviceId)
    {
        using var connection = _connectionFactory.CreateConnection();
        var inputs = await connection.QueryAsync<Input>(Sql.Inputs.GetByDevice, new { DeviceId = deviceId });
        
        return inputs.Select(i => new DeviceInputDto(
            i.PinIndex,
            i.LastState,
            i.UpdatedUtc,
            i.Name,
            i.NcLogic));
    }

    public async Task<Input?> GetByDevicePinAsync(int deviceId, byte pinIndex)
    {
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<Input>(Sql.Inputs.GetByDevicePin, new { DeviceId = deviceId, PinIndex = pinIndex });
    }

    public async Task UpsertAsync(int deviceId, byte pinIndex, bool lastState, DateTime updatedUtc)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            
            // Create a default input entry if it doesn't exist
            var parameters = new
            {
                DeviceId = deviceId,
                PinIndex = pinIndex,
                Name = $"P{pinIndex}",
                Enabled = true,
                NcLogic = true,
                DebounceMs = (short)0,
                LastState = lastState,
                UpdatedUtc = updatedUtc,
                LastChangeUtc = (DateTime?)null,
                Description = (string?)null
            };

            await connection.ExecuteAsync(Sql.Inputs.Upsert, parameters);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error upserting input for device ID: {deviceId}, pin: {pinIndex}", ex);
        }
    }

    public async Task UpdateSettingsAsync(int deviceId, byte pinIndex, UpdateInputDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var parameters = new
        {
            DeviceId = deviceId,
            PinIndex = pinIndex,
            dto.Name,
            dto.Enabled,
            dto.NcLogic,
            dto.DebounceMs,
            dto.Description
        };

        await connection.ExecuteAsync(Sql.Inputs.UpdateSettings, parameters);
    }
}