using IotIngest.Api.Auth;
using IotIngest.Api.Data;
using IotIngest.Api.Data.Repositories;
using IotIngest.Api.Endpoints;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "TeoriIot API",
        Version = "v1.0",
        Description = "High-performance IoT data ingestion system for ESP8266 devices",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "TeoriIot",
            Email = "info@teoriiot.com"
        }
    });

    // JWT Bearer Authentication
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer 12345abcdef'",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add database
var connectionString = builder.Configuration.GetConnectionString("Main")
    ?? throw new InvalidOperationException("Connection string 'Main' not found");

builder.Services.AddSingleton<IDbConnectionFactory>(new SqlServerConnectionFactory(connectionString));

// Add repositories
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<IInputRepository, InputRepository>();
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();

// Add authentication and authorization
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorizationPolicies();

// Add auth service
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// Configure the HTTP request pipeline
// Enable Swagger in all environments for API testing
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "TeoriIot API v1.0");
    options.RoutePrefix = "swagger"; // Set Swagger UI at /swagger
    options.DocumentTitle = "TeoriIot API Documentation";
    options.DisplayRequestDuration();
    
    // JWT Authentication in Swagger UI
    options.ConfigObject.AdditionalItems["syntaxHighlight"] = new Dictionary<string, object>
    {
        ["theme"] = "agate"
    };
});

app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
app.MapIngestEndpoints();
app.MapReadEndpoints();
app.MapAdminEndpoints();
app.MapAuthEndpoints();
app.MapUserEndpoints();

app.Run();

public partial class Program { }