using IotIngest.Api.Auth;
using IotIngest.Api.Data.Repositories;
using IotIngest.Api.Domain;

namespace IotIngest.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/users")
            .RequireAuthorization("AdminOnly");

        // Get all users
        group.MapGet("/", GetAllUsers)
            .WithName("GetAllUsers")
            .WithSummary("Get all users")
            .WithDescription("Returns list of all users with their roles and device scopes.")
            .WithTags("User Management")
            .Produces<IEnumerable<UserDto>>(200);

        // Get user by ID
        group.MapGet("/{id:int}", GetUserById)
            .WithName("GetUserById")
            .WithSummary("Get user by ID")
            .WithDescription("Returns a specific user with roles and device scopes.")
            .WithTags("User Management")
            .Produces<UserDto>(200)
            .Produces(404);

        // Create new user
        group.MapPost("/", CreateUser)
            .WithName("CreateUser")
            .WithSummary("Create new user")
            .WithDescription("Creates a new user with specified roles and device scopes.")
            .WithTags("User Management")
            .Accepts<CreateUserDto>("application/json")
            .Produces<UserDto>(201)
            .Produces(400)
            .Produces(409)
            .Produces(500);

        // Update user
        group.MapPut("/{id:int}", UpdateUser)
            .WithName("UpdateUser")
            .WithSummary("Update user")
            .WithDescription("Updates user information, roles, and device scopes.")
            .WithTags("User Management")
            .Accepts<UpdateUserDto>("application/json")
            .Produces<UserDto>(200)
            .Produces(404);

        // Delete user
        group.MapDelete("/{id:int}", DeleteUser)
            .WithName("DeleteUser")
            .WithSummary("Delete user")
            .WithDescription("Deletes a user and all associated data.")
            .WithTags("User Management")
            .Produces(204)
            .Produces(404);

        // Get all roles
        group.MapGet("/roles", GetAllRoles)
            .WithName("GetAllRoles")
            .WithSummary("Get all roles")
            .WithDescription("Returns list of all available roles.")
            .WithTags("User Management")
            .Produces<IEnumerable<RoleDto>>(200);
    }

    private static async Task<IResult> GetAllUsers(IAuthRepository authRepo)
    {
        var users = await authRepo.GetAllUsersAsync();
        return Results.Ok(users);
    }

    private static async Task<IResult> GetUserById(int id, IAuthRepository authRepo)
    {
        var user = await authRepo.GetUserByIdAsync(id);
        return user != null ? Results.Ok(user) : Results.NotFound($"User with ID {id} not found");
    }

    private static async Task<IResult> CreateUser(
        CreateUserDto dto,
        IAuthRepository authRepo,
        ILogger<Program> logger)
    {
        try
        {
            // Hash password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            
            // Create user
            var userId = await authRepo.CreateUserAsync(dto, passwordHash);
            
            // Get role IDs
            var allRoles = await authRepo.GetAllRolesAsync();
            var roleIds = allRoles.Where(r => dto.RoleNames.Contains(r.Name)).Select(r => r.Id).ToArray();
            
            // Set roles
            if (roleIds.Length > 0)
            {
                await authRepo.SetUserRolesAsync(userId, roleIds);
            }
            
            // Set device scopes
            if (dto.DeviceIds?.Length > 0)
            {
                await authRepo.SetUserDeviceScopesAsync(userId, dto.DeviceIds);
            }
            
            // Return created user
            var user = await authRepo.GetUserByIdAsync(userId);
            
            logger.LogInformation("User created: {Username} (ID: {UserId})", dto.Username, userId);
            return Results.Created($"/users/{userId}", user);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            logger.LogWarning("Duplicate username attempt: {Username}", dto.Username);
            return Results.Conflict(new { 
                error = "Username already exists", 
                message = ex.Message 
            });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Business logic error creating user: {Username}", dto.Username);
            return Results.BadRequest(new { 
                error = "Invalid operation", 
                message = ex.Message 
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error creating user: {Username}", dto.Username);
            return Results.Problem(
                title: "Internal server error", 
                detail: "An unexpected error occurred while creating the user",
                statusCode: 500);
        }
    }

    private static async Task<IResult> UpdateUser(
        int id,
        UpdateUserDto dto,
        IAuthRepository authRepo,
        ILogger<Program> logger)
    {
        try
        {
            var existingUser = await authRepo.GetUserByIdAsync(id);
            if (existingUser == null)
            {
                return Results.NotFound($"User with ID {id} not found");
            }

            // Hash password if provided
            string? passwordHash = null;
            if (!string.IsNullOrEmpty(dto.Password))
            {
                passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            }

            // Update user
            await authRepo.UpdateUserAsync(id, dto, passwordHash);
            
            // Get role IDs
            var allRoles = await authRepo.GetAllRolesAsync();
            var roleIds = allRoles.Where(r => dto.RoleNames.Contains(r.Name)).Select(r => r.Id).ToArray();
            
            // Update roles
            await authRepo.SetUserRolesAsync(id, roleIds);
            
            // Update device scopes
            await authRepo.SetUserDeviceScopesAsync(id, dto.DeviceIds ?? Array.Empty<int>());
            
            // Return updated user
            var user = await authRepo.GetUserByIdAsync(id);
            
            logger.LogInformation("User updated: ID={UserId}", id);
            return Results.Ok(user);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating user: ID={UserId}", id);
            return Results.Problem("Error updating user");
        }
    }

    private static async Task<IResult> DeleteUser(
        int id,
        IAuthRepository authRepo,
        ILogger<Program> logger)
    {
        try
        {
            var existingUser = await authRepo.GetUserByIdAsync(id);
            if (existingUser == null)
            {
                return Results.NotFound($"User with ID {id} not found");
            }

            await authRepo.DeleteUserAsync(id);
            
            logger.LogInformation("User deleted: ID={UserId}", id);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting user: ID={UserId}", id);
            return Results.Problem("Error deleting user");
        }
    }

    private static async Task<IResult> GetAllRoles(IAuthRepository authRepo)
    {
        var roles = await authRepo.GetAllRolesAsync();
        return Results.Ok(roles);
    }
}