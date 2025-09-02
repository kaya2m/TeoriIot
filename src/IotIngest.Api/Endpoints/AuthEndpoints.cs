using IotIngest.Api.Auth;
using IotIngest.Api.Domain;

namespace IotIngest.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth");

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("Authenticate user and get JWT tokens")
            .WithDescription("Authenticates user credentials and returns access/refresh tokens.")
            .WithTags("Authentication")
            .Accepts<LoginDto>("application/json")
            .Produces<LoginResponseDto>(200)
            .Produces(401);

        group.MapPost("/refresh", RefreshToken)
            .WithName("RefreshToken")
            .WithSummary("Refresh access token using refresh token")
            .WithDescription("Exchanges refresh token for new access/refresh token pair.")
            .WithTags("Authentication")
            .Accepts<RefreshDto>("application/json")
            .Produces<LoginResponseDto>(200)
            .Produces(401);

        group.MapPost("/logout", Logout)
            .WithName("Logout")
            .WithSummary("Revoke refresh token")
            .WithDescription("Revokes the provided refresh token.")
            .WithTags("Authentication")
            .Accepts<RefreshDto>("application/json")
            .Produces(200);
    }

    private static async Task<IResult> Login(
        LoginDto loginDto,
        IAuthService authService,
        ILogger<Program> logger)
    {
        if (string.IsNullOrEmpty(loginDto.Username) || string.IsNullOrEmpty(loginDto.Password))
        {
            return Results.BadRequest("Username and password are required");
        }

        var response = await authService.LoginAsync(loginDto);
        if (response == null)
        {
            logger.LogWarning("Failed login attempt for username: {Username}", loginDto.Username);
            return Results.Unauthorized();
        }

        logger.LogInformation("User logged in successfully: {Username}", loginDto.Username);
        return Results.Ok(response);
    }

    private static async Task<IResult> RefreshToken(
        RefreshDto refreshDto,
        IAuthService authService,
        ILogger<Program> logger)
    {
        if (string.IsNullOrEmpty(refreshDto.RefreshToken))
        {
            return Results.BadRequest("Refresh token is required");
        }

        var response = await authService.RefreshTokenAsync(refreshDto.RefreshToken);
        if (response == null)
        {
            logger.LogWarning("Invalid refresh token attempt");
            return Results.Unauthorized();
        }

        logger.LogDebug("Token refreshed successfully");
        return Results.Ok(response);
    }

    private static async Task<IResult> Logout(
        RefreshDto refreshDto,
        IAuthService authService,
        ILogger<Program> logger)
    {
        if (string.IsNullOrEmpty(refreshDto.RefreshToken))
        {
            return Results.BadRequest("Refresh token is required");
        }

        await authService.LogoutAsync(refreshDto.RefreshToken);
        
        logger.LogInformation("User logged out successfully");
        return Results.Ok(new { Message = "Logged out successfully" });
    }
}