using IotIngest.Api.Auth;
using IotIngest.Api.Data.Repositories;
using IotIngest.Api.Domain;

namespace IotIngest.Api.Endpoints;

public static class ReadEndpoints
{
    public static void MapReadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("")
            .RequireAuthorization("ViewerOrHigher");

        group.MapGet("/devices", GetDevices)
            .WithName("GetDevices")
            .WithSummary("Get all devices (filtered by user scopes if applicable)");

        group.MapGet("/devices/{node:int}/inputs", GetDeviceInputs)
            .WithName("GetDeviceInputs")
            .WithSummary("Get input states for a specific device node");

        group.MapGet("/events", GetEvents)
            .WithName("GetEvents")
            .WithSummary("Get recent events for a device node");

        group.MapGet("/health", () => Results.Ok(new { Status = "OK", Timestamp = DateTime.UtcNow }))
            .WithName("HealthCheck")
            .WithSummary("Health check endpoint")
            .AllowAnonymous();
    }

    private static async Task<IResult> GetDevices(
        HttpContext context,
        IDeviceRepository deviceRepo,
        IAuthService authService)
    {
        var userContext = await authService.GetUserContextAsync(context.User);
        if (userContext == null)
        {
            return Results.Unauthorized();
        }

        IEnumerable<DeviceDto> devices;

        // If user has device scopes, filter by those devices
        if (userContext.DeviceIds.Length > 0)
        {
            devices = await deviceRepo.GetForUserAsync(userContext.UserId);
        }
        else
        {
            // Admin or user without device scopes sees all devices
            devices = await deviceRepo.GetAllAsync();
        }

        return Results.Ok(devices);
    }

    private static async Task<IResult> GetDeviceInputs(
        int node,
        HttpContext context,
        IDeviceRepository deviceRepo,
        IInputRepository inputRepo,
        IAuthService authService)
    {
        var userContext = await authService.GetUserContextAsync(context.User);
        if (userContext == null)
        {
            return Results.Unauthorized();
        }

        // Get device by node number
        var device = await deviceRepo.GetByNodeNumAsync(node);
        if (device == null)
        {
            return Results.NotFound($"Device with node {node} not found");
        }

        // Check if user has access to this device
        if (userContext.DeviceIds.Length > 0 && !userContext.DeviceIds.Contains(device.Id))
        {
            return Results.Forbid();
        }

        var inputs = await inputRepo.GetByDeviceAsync(device.Id);
        return Results.Ok(inputs);
    }

    private static async Task<IResult> GetEvents(
        int? node,
        int? minutes,
        HttpContext context,
        IDeviceRepository deviceRepo,
        IEventRepository eventRepo,
        IAuthService authService)
    {
        if (!node.HasValue)
        {
            return Results.BadRequest("Node parameter is required");
        }

        var userContext = await authService.GetUserContextAsync(context.User);
        if (userContext == null)
        {
            return Results.Unauthorized();
        }

        // Validate minutes parameter
        var minutesToQuery = minutes ?? 60; // Default to 60 minutes
        if (minutesToQuery < 1 || minutesToQuery > 1440) // Max 24 hours
        {
            return Results.BadRequest("Minutes must be between 1 and 1440");
        }

        // Get device by node number
        var device = await deviceRepo.GetByNodeNumAsync(node.Value);
        if (device == null)
        {
            return Results.NotFound($"Device with node {node} not found");
        }

        // Check if user has access to this device
        if (userContext.DeviceIds.Length > 0 && !userContext.DeviceIds.Contains(device.Id))
        {
            return Results.Forbid();
        }

        var events = await eventRepo.GetRecentByNodeAsync(node.Value, minutesToQuery);
        return Results.Ok(events);
    }
}