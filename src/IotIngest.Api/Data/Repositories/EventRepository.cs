using Dapper;
using IotIngest.Api.Domain;

namespace IotIngest.Api.Data.Repositories;

public interface IEventRepository
{
    Task CreateAsync(int deviceId, byte? pinIndex, bool? state, int rawKapilarId, string? remoteIp);
    Task<IEnumerable<EventDto>> GetRecentByNodeAsync(int nodeNum, int minutes);
}

public class EventRepository : IEventRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public EventRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task CreateAsync(int deviceId, byte? pinIndex, bool? state, int rawKapilarId, string? remoteIp)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            
            var parameters = new
            {
                DeviceId = deviceId,
                PinIndex = pinIndex,
                State = state,
                RawKapilarId = rawKapilarId,
                RemoteIp = remoteIp
            };

            await connection.ExecuteAsync(Sql.Events.Create, parameters);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error creating event for device ID: {deviceId}, KapilarID: {rawKapilarId}", ex);
        }
    }

    public async Task<IEnumerable<EventDto>> GetRecentByNodeAsync(int nodeNum, int minutes)
    {
        using var connection = _connectionFactory.CreateConnection();
        
        var events = await connection.QueryAsync<Event>(Sql.Events.GetRecentByNode, new { NodeNum = nodeNum, Minutes = minutes });
        
        return events.Select(e => new EventDto(
            e.Id,
            e.DeviceId,
            e.PinIndex,
            e.State,
            e.RawKapilarId,
            e.RemoteIp,
            e.CreatedUtc));
    }
}