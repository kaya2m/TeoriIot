using IotIngest.Api.Auth;
using IotIngest.Api.Data.Repositories;
using IotIngest.Api.Domain;
using Microsoft.Extensions.Options;
using System.Net;

namespace IotIngest.Api.Endpoints;

public static class IngestEndpoints
{
    public static void MapIngestEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/ingest");

        group.MapPost("/", HandleFormIngest)
            .WithName("IngestForm")
            .WithSummary("ESP8266 form-urlencoded ingest endpoint")
            .WithDescription("Receives IoT device data via form-urlencoded POST. Returns 204 No Content.")
            .WithTags("ESP8266 Ingest")
            .Accepts<string>("application/x-www-form-urlencoded")
            .Produces(204);

        group.MapPost("/batch", HandleBatchIngest)
            .WithName("IngestBatch")
            .WithSummary("Batch JSON ingest endpoint")
            .WithDescription("Receives multiple IoT device readings in a single JSON request.")
            .WithTags("ESP8266 Ingest")
            .Accepts<BatchIngestDto>("application/json")
            .Produces(204);
    }

    private static async Task<IResult> HandleFormIngest(
        HttpContext context,
        IDeviceRepository deviceRepo,
        IInputRepository inputRepo,
        IEventRepository eventRepo,
        IOptions<IngestOptions> ingestOptions,
        ILogger<Program> logger)
    {
        try
        {
            // Parse form data
            var form = await context.Request.ReadFormAsync();
            
            if (!int.TryParse(form["kapilar_id"], out var kapilarId))
            {
                return Results.BadRequest("Invalid kapilar_id");
            }

            if (!int.TryParse(form["durum"], out var durum))
            {
                return Results.BadRequest("Invalid durum");
            }

            var pass = form["pass"].ToString();
            var remoteIp = GetRemoteIpAddress(context);

            // Decode kapilar_id to get node and pin
            var (nodeNum, pinIndex) = KapilarDecoder.Decode(kapilarId);

            // Validate device and API key
            var device = await deviceRepo.GetActiveByNodeNumAsync(nodeNum);
            if (device == null)
            {
                logger.LogWarning("Device not found or inactive: NodeNum={NodeNum}, IP={RemoteIp}", nodeNum, remoteIp);
                return Results.NoContent(); // Return 204 even on error for security
            }

            // Check API key (device key or master key)
            var masterKey = ingestOptions.Value.MasterKey;
            var isValidKey = device.ApiKey == pass || 
                           (!string.IsNullOrEmpty(masterKey) && masterKey == pass);

            if (!isValidKey)
            {
                logger.LogWarning("Invalid API key for device: NodeNum={NodeNum}, IP={RemoteIp}", nodeNum, remoteIp);
                return Results.NoContent(); // Return 204 even on error for security
            }

            var now = DateTime.UtcNow;

            // Create event record
            await eventRepo.CreateAsync(device.Id, pinIndex, pinIndex.HasValue ? durum == 1 : null, kapilarId, remoteIp);

            if (pinIndex.HasValue)
            {
                // Normal pin input - update input state
                var state = durum == 1;
                await inputRepo.UpsertAsync(device.Id, pinIndex.Value, state, now);
                
                logger.LogDebug("Pin input processed: Node={NodeNum}, Pin={PinIndex}, State={State}", 
                    nodeNum, pinIndex.Value, state);
            }
            else
            {
                // Heartbeat
                logger.LogDebug("Heartbeat processed: Node={NodeNum}", nodeNum);
            }

            // Update device last seen
            await deviceRepo.UpdateLastSeenAsync(device.Id, remoteIp);

            return Results.NoContent();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            logger.LogWarning(ex, "Invalid kapilar_id in ingest request");
            return Results.NoContent(); // Return 204 even on error for security
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing ingest request");
            return Results.NoContent(); // Return 204 even on error for security
        }
    }

    private static async Task<IResult> HandleBatchIngest(
        HttpContext context,
        BatchIngestDto batchDto,
        IDeviceRepository deviceRepo,
        IInputRepository inputRepo,
        IEventRepository eventRepo,
        IOptions<IngestOptions> ingestOptions,
        ILogger<Program> logger)
    {
        try
        {
            var deviceKey = context.Request.Headers["X-DEVICE-KEY"].FirstOrDefault();
            if (string.IsNullOrEmpty(deviceKey))
            {
                return Results.NoContent(); // Return 204 even on error for security
            }

            var remoteIp = GetRemoteIpAddress(context);
            var now = DateTime.UtcNow;
            var masterKey = ingestOptions.Value.MasterKey;

            // Process each item in batch
            foreach (var item in batchDto.Items)
            {
                try
                {
                    var (nodeNum, pinIndex) = KapilarDecoder.Decode(item.KapilarId);

                    // Validate device and API key
                    var device = await deviceRepo.GetActiveByNodeNumAsync(nodeNum);
                    if (device == null) continue;

                    var isValidKey = device.ApiKey == deviceKey || 
                                   (!string.IsNullOrEmpty(masterKey) && masterKey == deviceKey);
                    if (!isValidKey) continue;

                    // Create event record
                    await eventRepo.CreateAsync(device.Id, pinIndex, pinIndex.HasValue ? item.Durum == 1 : null, item.KapilarId, remoteIp);

                    if (pinIndex.HasValue)
                    {
                        // Normal pin input
                        var state = item.Durum == 1;
                        await inputRepo.UpsertAsync(device.Id, pinIndex.Value, state, now);
                    }

                    // Update device last seen
                    await deviceRepo.UpdateLastSeenAsync(device.Id, remoteIp);

                    logger.LogDebug("Batch item processed: KapilarId={KapilarId}, Node={NodeNum}, Pin={PinIndex}", 
                        item.KapilarId, nodeNum, pinIndex);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error processing batch item: KapilarId={KapilarId}", item.KapilarId);
                    // Continue processing other items
                }
            }

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing batch ingest request");
            return Results.NoContent(); // Return 204 even on error for security
        }
    }

    private static string? GetRemoteIpAddress(HttpContext context)
    {
        // Check for forwarded IP first (behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ip = forwardedFor.Split(',')[0].Trim();
            if (IPAddress.TryParse(ip, out _))
            {
                return ip;
            }
        }

        // Check X-Real-IP header
        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp) && IPAddress.TryParse(realIp, out _))
        {
            return realIp;
        }

        // Use connection remote IP
        return context.Connection.RemoteIpAddress?.ToString();
    }
}