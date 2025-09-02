using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace IotIngest.Api.Auth;

public static class JwtExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing");

        if (string.IsNullOrEmpty(jwtOptions.Key) || jwtOptions.Key.Length < 32)
        {
            throw new InvalidOperationException("JWT Key must be at least 256 bits (32 characters) long");
        }

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.Section));
        services.Configure<IngestOptions>(configuration.GetSection(IngestOptions.Section));

        var key = Encoding.UTF8.GetBytes(jwtOptions.Key);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });

        return services;
    }

    public static IServiceCollection AddAuthorizationPolicies(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("ViewerOrHigher", policy =>
                policy.RequireAuthenticatedUser().RequireClaim("role_level").RequireAssertion(context =>
                {
                    var levelClaim = context.User.FindFirst("role_level")?.Value;
                    return int.TryParse(levelClaim, out var level) && level >= 10;
                }));

            options.AddPolicy("OperatorOrHigher", policy =>
                policy.RequireAuthenticatedUser().RequireClaim("role_level").RequireAssertion(context =>
                {
                    var levelClaim = context.User.FindFirst("role_level")?.Value;
                    return int.TryParse(levelClaim, out var level) && level >= 50;
                }));

            options.AddPolicy("AdminOnly", policy =>
                policy.RequireAuthenticatedUser().RequireClaim("role_level").RequireAssertion(context =>
                {
                    var levelClaim = context.User.FindFirst("role_level")?.Value;
                    return int.TryParse(levelClaim, out var level) && level >= 100;
                }));
        });

        return services;
    }
}