namespace IotIngest.Api.Domain;

public record LoginDto(string Username, string Password);

public record LoginResponseDto(
    string AccessToken, 
    string RefreshToken, 
    DateTime ExpiresAt);

public record RefreshDto(string RefreshToken);

public record BatchItem(int KapilarId, int Durum);

public record BatchIngestDto(BatchItem[] Items);

public record DeviceDto(
    int Id,
    int NodeNum,
    string? Name,
    bool IsActive,
    DateTime? LastSeenUtc);

public record DeviceInputDto(
    byte PinIndex,
    bool LastState,
    DateTime UpdatedUtc,
    string? Name,
    bool NcLogic);

public record EventDto(
    long Id,
    int DeviceId,
    byte? PinIndex,
    bool? State,
    int RawKapilarId,
    string? RemoteIp,
    DateTime CreatedUtc);

public record CreateDeviceDto(
    int NodeNum,
    string? Name,
    string ApiKey,
    bool IsActive);

public record UpdateInputDto(
    string? Name,
    bool Enabled,
    bool NcLogic,
    short DebounceMs,
    string? Description);

public record UserContext(
    int UserId,
    string Username,
    int RoleLevel,
    int[] DeviceIds);

public record CreateUserDto(
    string Username,
    string? DisplayName,
    string Password,
    bool IsActive,
    string[] RoleNames,
    int[]? DeviceIds);

public record UpdateUserDto(
    string? DisplayName,
    string? Password,
    bool IsActive,
    string[] RoleNames,
    int[]? DeviceIds);

public record UserDto(
    int Id,
    string Username,
    string? DisplayName,
    bool IsActive,
    DateTime? LastLoginUtc,
    DateTime CreatedUtc,
    string[] RoleNames,
    int[] DeviceIds);

public record RoleDto(
    int Id,
    string Name,
    short Level);

// Internal domain models
public class Device
{
    public int Id { get; set; }
    public int NodeNum { get; set; }
    public string? Name { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public bool IsActive { get; set; }
    public string? FirmwareVersion { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class Input
{
    public int DeviceId { get; set; }
    public byte PinIndex { get; set; }
    public string? Name { get; set; }
    public bool Enabled { get; set; }
    public bool NcLogic { get; set; }
    public short DebounceMs { get; set; }
    public bool LastState { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? LastChangeUtc { get; set; }
    public string? Description { get; set; }
}

public class Event
{
    public long Id { get; set; }
    public int DeviceId { get; set; }
    public byte? PinIndex { get; set; }
    public bool? State { get; set; }
    public int RawKapilarId { get; set; }
    public string? RemoteIp { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class AuthUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastLoginUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public class AuthRole
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public short Level { get; set; }
}

public class AuthRefreshToken
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public string? ReplacedByHash { get; set; }
}