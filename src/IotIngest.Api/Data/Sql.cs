namespace IotIngest.Api.Data;

public static class Sql
{
    public static class Devices
    {
        public const string GetByNodeNum = @"
            SELECT Id, NodeNum, Name, ApiKey, IpAddress, IsActive, FirmwareVersion, LastSeenUtc, CreatedUtc
            FROM iot.Devices 
            WHERE NodeNum = @NodeNum";

        public const string GetActiveByNodeNum = @"
            SELECT Id, NodeNum, Name, ApiKey, IpAddress, IsActive, FirmwareVersion, LastSeenUtc, CreatedUtc
            FROM iot.Devices 
            WHERE NodeNum = @NodeNum AND IsActive = 1";

        public const string GetAll = @"
            SELECT Id, NodeNum, Name, IsActive, LastSeenUtc
            FROM iot.Devices 
            ORDER BY NodeNum";

        public const string GetForUser = @"
            SELECT d.Id, d.NodeNum, d.Name, d.IsActive, d.LastSeenUtc
            FROM iot.Devices d
            INNER JOIN iot.UserDeviceScopes uds ON d.Id = uds.DeviceId
            WHERE uds.UserId = @UserId
            ORDER BY d.NodeNum";

        public const string UpdateLastSeen = @"
            UPDATE iot.Devices 
            SET LastSeenUtc = SYSUTCDATETIME(), IpAddress = @IpAddress
            WHERE Id = @DeviceId";

        public const string Create = @"
            INSERT INTO iot.Devices (NodeNum, Name, ApiKey, IsActive)
            VALUES (@NodeNum, @Name, @ApiKey, @IsActive)";

        public const string Update = @"
            UPDATE iot.Devices 
            SET Name = @Name, ApiKey = @ApiKey, IsActive = @IsActive
            WHERE Id = @Id";
    }

    public static class Inputs
    {
        public const string GetByDevice = @"
            SELECT DeviceId, PinIndex, Name, Enabled, NcLogic, DebounceMs, LastState, UpdatedUtc, LastChangeUtc, Description
            FROM iot.Inputs 
            WHERE DeviceId = @DeviceId
            ORDER BY PinIndex";

        public const string GetByDevicePin = @"
            SELECT DeviceId, PinIndex, Name, Enabled, NcLogic, DebounceMs, LastState, UpdatedUtc, LastChangeUtc, Description
            FROM iot.Inputs 
            WHERE DeviceId = @DeviceId AND PinIndex = @PinIndex";

        public const string Upsert = @"
            MERGE iot.Inputs AS target
            USING (VALUES (@DeviceId, @PinIndex, @Name, @Enabled, @NcLogic, @DebounceMs, @LastState, @UpdatedUtc, @LastChangeUtc, @Description)) 
                AS source (DeviceId, PinIndex, Name, Enabled, NcLogic, DebounceMs, LastState, UpdatedUtc, LastChangeUtc, Description)
            ON target.DeviceId = source.DeviceId AND target.PinIndex = source.PinIndex
            WHEN MATCHED THEN
                UPDATE SET 
                    LastState = source.LastState,
                    UpdatedUtc = source.UpdatedUtc,
                    LastChangeUtc = CASE WHEN target.LastState != source.LastState THEN source.UpdatedUtc ELSE target.LastChangeUtc END
            WHEN NOT MATCHED THEN
                INSERT (DeviceId, PinIndex, Name, Enabled, NcLogic, DebounceMs, LastState, UpdatedUtc, LastChangeUtc, Description)
                VALUES (source.DeviceId, source.PinIndex, source.Name, source.Enabled, source.NcLogic, source.DebounceMs, source.LastState, source.UpdatedUtc, source.LastChangeUtc, source.Description);";

        public const string UpdateSettings = @"
            UPDATE iot.Inputs 
            SET Name = @Name, Enabled = @Enabled, NcLogic = @NcLogic, DebounceMs = @DebounceMs, Description = @Description
            WHERE DeviceId = @DeviceId AND PinIndex = @PinIndex";
    }

    public static class Events
    {
        public const string Create = @"
            INSERT INTO iot.Events (DeviceId, PinIndex, State, RawKapilarId, RemoteIp)
            VALUES (@DeviceId, @PinIndex, @State, @RawKapilarId, @RemoteIp)";

        public const string GetByDeviceRecent = @"
            SELECT TOP 1000 Id, DeviceId, PinIndex, State, RawKapilarId, RemoteIp, CreatedUtc
            FROM iot.Events 
            WHERE DeviceId = @DeviceId AND CreatedUtc >= DATEADD(MINUTE, -@Minutes, SYSUTCDATETIME())
            ORDER BY CreatedUtc DESC";

        public const string GetRecentByNode = @"
            SELECT TOP 1000 e.Id, e.DeviceId, e.PinIndex, e.State, e.RawKapilarId, e.RemoteIp, e.CreatedUtc
            FROM iot.Events e
            INNER JOIN iot.Devices d ON e.DeviceId = d.Id
            WHERE d.NodeNum = @NodeNum AND e.CreatedUtc >= DATEADD(MINUTE, -@Minutes, SYSUTCDATETIME())
            ORDER BY e.CreatedUtc DESC";
    }

    public static class Auth
    {
        public const string GetUserByUsername = @"
            SELECT u.Id, u.Username, u.DisplayName, u.PasswordHash, u.IsActive, u.LastLoginUtc, u.CreatedUtc
            FROM iot.AuthUsers u 
            WHERE u.Username = @Username AND u.IsActive = 1";

        public const string GetUserWithRoles = @"
            SELECT u.Id, u.Username, u.DisplayName, u.PasswordHash, u.IsActive, u.LastLoginUtc, u.CreatedUtc,
                   r.Id as RoleId, r.Name as RoleName, r.Level as RoleLevel
            FROM iot.AuthUsers u
            INNER JOIN iot.AuthUserRoles ur ON u.Id = ur.UserId
            INNER JOIN iot.AuthRoles r ON ur.RoleId = r.Id
            WHERE u.Username = @Username AND u.IsActive = 1";

        public const string GetUserDeviceScopes = @"
            SELECT DeviceId
            FROM iot.UserDeviceScopes 
            WHERE UserId = @UserId";

        public const string UpdateLastLogin = @"
            UPDATE iot.AuthUsers 
            SET LastLoginUtc = SYSUTCDATETIME() 
            WHERE Id = @UserId";

        public const string CreateRefreshToken = @"
            INSERT INTO iot.AuthRefreshTokens (UserId, TokenHash, ExpiresUtc)
            VALUES (@UserId, @TokenHash, @ExpiresUtc)";

        public const string GetRefreshToken = @"
            SELECT Id, UserId, TokenHash, ExpiresUtc, CreatedUtc, RevokedUtc, ReplacedByHash
            FROM iot.AuthRefreshTokens 
            WHERE TokenHash = @TokenHash AND ExpiresUtc > SYSUTCDATETIME() AND RevokedUtc IS NULL";

        public const string RevokeRefreshToken = @"
            UPDATE iot.AuthRefreshTokens 
            SET RevokedUtc = SYSUTCDATETIME(), ReplacedByHash = @ReplacedByHash
            WHERE Id = @Id";

        public const string RevokeUserTokens = @"
            UPDATE iot.AuthRefreshTokens 
            SET RevokedUtc = SYSUTCDATETIME()
            WHERE UserId = @UserId AND RevokedUtc IS NULL";

        // User Management
        public const string GetAllUsers = @"
            SELECT u.Id, u.Username, u.DisplayName, u.IsActive, u.LastLoginUtc, u.CreatedUtc
            FROM iot.AuthUsers u 
            ORDER BY u.CreatedUtc DESC";

        public const string GetUserById = @"
            SELECT u.Id, u.Username, u.DisplayName, u.IsActive, u.LastLoginUtc, u.CreatedUtc
            FROM iot.AuthUsers u 
            WHERE u.Id = @UserId";

        public const string GetAllRoles = @"
            SELECT Id, Name, Level 
            FROM iot.AuthRoles 
            ORDER BY Level";

        public const string CreateUser = @"
            INSERT INTO iot.AuthUsers (Username, DisplayName, PasswordHash, IsActive)
            VALUES (@Username, @DisplayName, @PasswordHash, @IsActive);
            SELECT SCOPE_IDENTITY();";

        public const string UpdateUser = @"
            UPDATE iot.AuthUsers 
            SET DisplayName = @DisplayName, IsActive = @IsActive
            WHERE Id = @Id";

        public const string UpdateUserPassword = @"
            UPDATE iot.AuthUsers 
            SET DisplayName = @DisplayName, IsActive = @IsActive, PasswordHash = @PasswordHash
            WHERE Id = @Id";

        public const string DeleteUser = @"
            DELETE FROM iot.AuthUsers WHERE Id = @UserId";

        public const string DeleteUserRoles = @"
            DELETE FROM iot.AuthUserRoles WHERE UserId = @UserId";

        public const string InsertUserRole = @"
            INSERT INTO iot.AuthUserRoles (UserId, RoleId) VALUES (@UserId, @RoleId)";

        public const string DeleteUserDeviceScopes = @"
            DELETE FROM iot.UserDeviceScopes WHERE UserId = @UserId";

        public const string InsertUserDeviceScope = @"
            INSERT INTO iot.UserDeviceScopes (UserId, DeviceId) VALUES (@UserId, @DeviceId)";

        public const string GetUserRoles = @"
            SELECT r.Id, r.Name, r.Level
            FROM iot.AuthRoles r
            INNER JOIN iot.AuthUserRoles ur ON r.Id = ur.RoleId
            WHERE ur.UserId = @UserId";
    }
}