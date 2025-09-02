using IotIngest.Api.Data.Repositories;
using IotIngest.Api.Domain;

namespace IotIngest.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var adminGroup = endpoints.MapGroup("/admin")
            .RequireAuthorization("AdminOnly");

        adminGroup.MapPost("/devices", CreateDevice)
            .WithName("CreateDevice")
            .WithSummary("Create a new device (Admin only)");

        adminGroup.MapPut("/devices/{id:int}", UpdateDevice)
            .WithName("UpdateDevice")
            .WithSummary("Update an existing device (Admin only)");

        var operatorGroup = endpoints.MapGroup("/admin")
            .RequireAuthorization("OperatorOrHigher");

        operatorGroup.MapPost("/inputs/{node:int}/{pin:int}", UpdateInput)
            .WithName("UpdateInputSettings")
            .WithSummary("Update input pin settings (Operator or higher)");
    }

    private static async Task<IResult> CreateDevice(
        CreateDeviceDto dto,
        IDeviceRepository deviceRepo,
        ILogger<Program> logger)
    {
        try
        {
            await deviceRepo.CreateAsync(dto);
            logger.LogInformation("Device created: NodeNum={NodeNum}, Name={Name}", dto.NodeNum, dto.Name);
            return Results.Created($"/devices/{dto.NodeNum}", dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating device: NodeNum={NodeNum}", dto.NodeNum);
            return Results.Problem("Error creating device");
        }
    }

    private static async Task<IResult> UpdateDevice(
        int id,
        CreateDeviceDto dto,
        IDeviceRepository deviceRepo,
        ILogger<Program> logger)
    {
        try
        {
            await deviceRepo.UpdateAsync(id, dto);
            logger.LogInformation("Device updated: Id={Id}, NodeNum={NodeNum}", id, dto.NodeNum);
            return Results.Ok(dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating device: Id={Id}", id);
            return Results.Problem("Error updating device");
        }
    }

    private static async Task<IResult> UpdateInput(
        int node,
        int pin,
        UpdateInputDto dto,
        IDeviceRepository deviceRepo,
        IInputRepository inputRepo,
        ILogger<Program> logger)
    {
        if (pin < 0 || pin > 7)
        {
            return Results.BadRequest("Pin must be between 0 and 7");
        }

        try
        {
            // Get device by node number
            var device = await deviceRepo.GetByNodeNumAsync(node);
            if (device == null)
            {
                return Results.NotFound($"Device with node {node} not found");
            }

            await inputRepo.UpdateSettingsAsync(device.Id, (byte)pin, dto);
            
            logger.LogInformation("Input settings updated: Node={Node}, Pin={Pin}", node, pin);
            return Results.Ok(dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating input settings: Node={Node}, Pin={Pin}", node, pin);
            return Results.Problem("Error updating input settings");
        }
    }
}