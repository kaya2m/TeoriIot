namespace IotIngest.Api.Auth;

public class JwtOptions
{
    public const string Section = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; set; } = 30;
    public int RefreshTokenExpirationDays { get; set; } = 14;
}

public class IngestOptions
{
    public const string Section = "Ingest";

    public string? MasterKey { get; set; }
}