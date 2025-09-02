-- TeoriIot Database Schema and Seed Data
-- This script creates the database and all tables

-- Create database if not exists
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'TeoriIot')
BEGIN
    CREATE DATABASE TeoriIot;
END
GO

USE TeoriIot;
GO

-- Create schema
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'iot')
BEGIN
    EXEC('CREATE SCHEMA iot')
END
GO

-- Drop existing tables in dependency order
IF OBJECT_ID('iot.AuthRefreshTokens', 'U') IS NOT NULL DROP TABLE iot.AuthRefreshTokens;
IF OBJECT_ID('iot.AuthUserRoles', 'U') IS NOT NULL DROP TABLE iot.AuthUserRoles;
IF OBJECT_ID('iot.UserDeviceScopes', 'U') IS NOT NULL DROP TABLE iot.UserDeviceScopes;
IF OBJECT_ID('iot.AuthUsers', 'U') IS NOT NULL DROP TABLE iot.AuthUsers;
IF OBJECT_ID('iot.AuthRoles', 'U') IS NOT NULL DROP TABLE iot.AuthRoles;
IF OBJECT_ID('iot.Alerts', 'U') IS NOT NULL DROP TABLE iot.Alerts;
IF OBJECT_ID('iot.AlertRules', 'U') IS NOT NULL DROP TABLE iot.AlertRules;
IF OBJECT_ID('iot.Events', 'U') IS NOT NULL DROP TABLE iot.Events;
IF OBJECT_ID('iot.Inputs', 'U') IS NOT NULL DROP TABLE iot.Inputs;
IF OBJECT_ID('iot.Devices', 'U') IS NOT NULL DROP TABLE iot.Devices;
GO

-- Create Devices table
CREATE TABLE iot.Devices (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    NodeNum INT NOT NULL UNIQUE,
    Name NVARCHAR(80) NULL,
    ApiKey NVARCHAR(128) NOT NULL,
    IpAddress VARCHAR(45) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    FirmwareVersion NVARCHAR(40) NULL,
    LastSeenUtc DATETIME2(3) NULL,
    CreatedUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);

-- Create Inputs table
CREATE TABLE iot.Inputs (
    DeviceId INT NOT NULL,
    PinIndex TINYINT NOT NULL,
    Name NVARCHAR(80) NULL,
    Enabled BIT NOT NULL DEFAULT 1,
    NcLogic BIT NOT NULL DEFAULT 1,
    DebounceMs SMALLINT NOT NULL DEFAULT 0,
    LastState BIT NOT NULL,
    UpdatedUtc DATETIME2(3) NOT NULL,
    LastChangeUtc DATETIME2(3) NULL,
    Description NVARCHAR(200) NULL,
    CONSTRAINT PK_Inputs PRIMARY KEY (DeviceId, PinIndex),
    CONSTRAINT FK_Inputs_Device FOREIGN KEY (DeviceId) REFERENCES iot.Devices(Id) ON DELETE CASCADE,
    CONSTRAINT CHK_Inputs_PinIndex CHECK (PinIndex BETWEEN 0 AND 7)
);

-- Create Events table
CREATE TABLE iot.Events (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    DeviceId INT NOT NULL,
    PinIndex TINYINT NULL,
    State BIT NULL,
    RawKapilarId INT NOT NULL,
    RemoteIp VARCHAR(45) NULL,
    CreatedUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Events_Device FOREIGN KEY (DeviceId) REFERENCES iot.Devices(Id) ON DELETE CASCADE,
    CONSTRAINT CHK_Events_ValidInput CHECK (
        (PinIndex IS NULL AND State IS NULL) OR 
        (PinIndex BETWEEN 0 AND 7 AND State IN (0,1))
    )
);

-- Create AuthRoles table
CREATE TABLE iot.AuthRoles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(40) NOT NULL UNIQUE,
    Level SMALLINT NOT NULL,
    CONSTRAINT CHK_AuthRoles_Level CHECK (Level IN (10, 50, 100))
);

-- Create AuthUsers table
CREATE TABLE iot.AuthUsers (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(60) NOT NULL UNIQUE,
    DisplayName NVARCHAR(80) NULL,
    PasswordHash NVARCHAR(200) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    LastLoginUtc DATETIME2(3) NULL,
    CreatedUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);

-- Create AuthUserRoles table
CREATE TABLE iot.AuthUserRoles (
    UserId INT NOT NULL,
    RoleId INT NOT NULL,
    CONSTRAINT PK_AuthUserRoles PRIMARY KEY (UserId, RoleId),
    CONSTRAINT FK_AuthUserRoles_User FOREIGN KEY (UserId) REFERENCES iot.AuthUsers(Id) ON DELETE CASCADE,
    CONSTRAINT FK_AuthUserRoles_Role FOREIGN KEY (RoleId) REFERENCES iot.AuthRoles(Id) ON DELETE CASCADE
);

-- Create UserDeviceScopes table (optional)
CREATE TABLE iot.UserDeviceScopes (
    UserId INT NOT NULL,
    DeviceId INT NOT NULL,
    CONSTRAINT PK_UserDeviceScopes PRIMARY KEY (UserId, DeviceId),
    CONSTRAINT FK_UserDeviceScopes_User FOREIGN KEY (UserId) REFERENCES iot.AuthUsers(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserDeviceScopes_Device FOREIGN KEY (DeviceId) REFERENCES iot.Devices(Id) ON DELETE CASCADE
);

-- Create AuthRefreshTokens table
CREATE TABLE iot.AuthRefreshTokens (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    TokenHash CHAR(64) NOT NULL,
    ExpiresUtc DATETIME2(3) NOT NULL,
    CreatedUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    RevokedUtc DATETIME2(3) NULL,
    ReplacedByHash CHAR(64) NULL,
    CONSTRAINT FK_AuthRefreshTokens_User FOREIGN KEY (UserId) REFERENCES iot.AuthUsers(Id) ON DELETE CASCADE
);

-- Create AlertRules table (optional)
CREATE TABLE iot.AlertRules (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    DeviceId INT NULL,
    PinIndex TINYINT NULL,
    RuleType TINYINT NOT NULL, -- 1=StateChange, 2=Offline
    MatchState BIT NULL,
    OfflineAfterS INT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_AlertRules_Device FOREIGN KEY (DeviceId) REFERENCES iot.Devices(Id) ON DELETE CASCADE,
    CONSTRAINT CHK_AlertRules_RuleType CHECK (RuleType IN (1, 2)),
    CONSTRAINT CHK_AlertRules_PinIndex CHECK (PinIndex IS NULL OR PinIndex BETWEEN 0 AND 7)
);

-- Create Alerts table (optional)
CREATE TABLE iot.Alerts (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    RuleId INT NOT NULL,
    DeviceId INT NOT NULL,
    PinIndex TINYINT NULL,
    TriggeredUtc DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    ResolvedUtc DATETIME2(3) NULL,
    Message NVARCHAR(500) NULL,
    CONSTRAINT FK_Alerts_Rule FOREIGN KEY (RuleId) REFERENCES iot.AlertRules(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Alerts_Device FOREIGN KEY (DeviceId) REFERENCES iot.Devices(Id) ON DELETE NO ACTION
);

-- Create indexes for performance
CREATE INDEX IX_Inputs_DevicePin ON iot.Inputs (DeviceId, PinIndex);
CREATE INDEX IX_Inputs_Updated ON iot.Inputs (UpdatedUtc DESC);

CREATE INDEX IX_Events_Device_Created ON iot.Events (DeviceId, CreatedUtc DESC);
CREATE INDEX IX_Events_Device_Pin_Created ON iot.Events (DeviceId, PinIndex, CreatedUtc DESC) WHERE PinIndex IS NOT NULL;

CREATE INDEX IX_AuthRefreshTokens_User_Expires ON iot.AuthRefreshTokens (UserId, ExpiresUtc) WHERE RevokedUtc IS NULL;

-- Seed data: Insert AuthRoles
INSERT INTO iot.AuthRoles (Name, Level) VALUES
('Viewer', 10),
('Operator', 50),
('Admin', 100);

-- Seed data: Insert admin user with bcrypt hashed password for 'ChangeMe123!'
-- Hash generated with BCrypt.Net-Next using default work factor
INSERT INTO iot.AuthUsers (Username, DisplayName, PasswordHash) VALUES 
('admin', 'System Administrator', '$2a$11$6EwI8iBFyS3Dj6Wd4wLVB.Ub2z7CbJ8ynl5hFGpHJ4qVQ2ZRxKdJm');

-- Assign admin role to admin user
INSERT INTO iot.AuthUserRoles (UserId, RoleId)
SELECT u.Id, r.Id
FROM iot.AuthUsers u, iot.AuthRoles r
WHERE u.Username = 'admin' AND r.Name = 'Admin';

-- Seed data: Insert sample device
INSERT INTO iot.Devices (NodeNum, Name, ApiKey) VALUES 
(0, 'Atölye Giriş', 'YOUR_FIRMWARE_PASS');

-- Seed data: Insert input pins for device 0 (P0-P7)
DECLARE @DeviceId INT = (SELECT Id FROM iot.Devices WHERE NodeNum = 0);
INSERT INTO iot.Inputs (DeviceId, PinIndex, Name, Enabled, NcLogic, LastState, UpdatedUtc) VALUES
(@DeviceId, 0, 'P0', 1, 1, 0, SYSUTCDATETIME()),
(@DeviceId, 1, 'P1', 1, 1, 0, SYSUTCDATETIME()),
(@DeviceId, 2, 'P2', 1, 1, 0, SYSUTCDATETIME()),
(@DeviceId, 3, 'P3', 1, 1, 0, SYSUTCDATETIME()),
(@DeviceId, 4, 'P4', 1, 1, 0, SYSUTCDATETIME()),
(@DeviceId, 5, 'P5', 1, 1, 0, SYSUTCDATETIME()),
(@DeviceId, 6, 'P6', 1, 1, 0, SYSUTCDATETIME()),
(@DeviceId, 7, 'P7', 1, 1, 0, SYSUTCDATETIME());

GO

PRINT 'TeoriIot database and schema created successfully';
PRINT 'Admin user credentials: admin / ChangeMe123!';
PRINT 'Sample device API key: YOUR_FIRMWARE_PASS';
GO