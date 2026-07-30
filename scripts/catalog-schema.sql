-- =============================================================
-- CATALOG DATABASE (SQL Server)
-- Control plane: stores metadata for cells, tenants, users,
-- provisioning, and audit. All business logic lives in SPs/Views.
-- =============================================================

-- =============================================================
-- SCHEMA
-- =============================================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'catalog')
    EXEC('CREATE SCHEMA catalog');
GO

-- =============================================================
-- TABLES
-- =============================================================

CREATE TABLE catalog.Providers (
    ProviderId   INT           IDENTITY(1,1) PRIMARY KEY,
    Name         NVARCHAR(50)  NOT NULL UNIQUE,   -- 'Google', 'GitHub'
    CreatedAt    DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);

INSERT INTO catalog.Providers (Name) VALUES ('Google'), ('GitHub');

CREATE TABLE catalog.Users (
    UserId        INT            IDENTITY(1,1) PRIMARY KEY,
    Email         NVARCHAR(255)  NOT NULL,
    Name          NVARCHAR(255)  NOT NULL,
    AvatarUrl     NVARCHAR(500)  NULL,
    ProviderId    INT            NULL,              -- NULL for email/password users
    ProviderUserId NVARCHAR(255) NULL,              -- sub from OAuth
    PasswordHash  NVARCHAR(500)  NULL,              -- NULL for OAuth users
    TenantId      INT            NULL,              -- NULL until provisioning
    Role          NVARCHAR(50)   NOT NULL DEFAULT 'User',  -- 'User' | 'SuperAdmin'
    CreatedAt     DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    LastLoginAt   DATETIME2      NULL,

    CONSTRAINT UQ_Users_Provider UNIQUE (ProviderId, ProviderUserId),
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);

CREATE TABLE catalog.Cells (
    CellId      INT           IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(100) NOT NULL UNIQUE,
    Slug        NVARCHAR(100) NOT NULL UNIQUE,
    CreatedAt   DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE catalog.Tenants (
    TenantId        INT            IDENTITY(1,1) PRIMARY KEY,
    CellId          INT            NOT NULL REFERENCES catalog.Cells(CellId),
    Slug            NVARCHAR(100)  NOT NULL,
    DbName          NVARCHAR(255)  NOT NULL,
    DbUser          NVARCHAR(255)  NOT NULL,
    PasswordEncrypted NVARCHAR(500) NULL,
    Host            NVARCHAR(255)  NOT NULL,
    Port            INT            NOT NULL DEFAULT 3306,
    Engine          NVARCHAR(50)   NOT NULL DEFAULT 'MySQL',
    MaxSizeBytes    BIGINT         NOT NULL DEFAULT 20971520,  -- 20 MB
    CurrentSizeBytes BIGINT        NOT NULL DEFAULT 0,
    IsActive        BIT            NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME(),
    LastActivityAt  DATETIME2      NULL,
    UserId          INT            NOT NULL REFERENCES catalog.Users(UserId),

    CONSTRAINT UQ_Tenants_CellSlug UNIQUE (CellId, Slug)
);

CREATE TABLE catalog.ProvisioningAudit (
    AuditId      INT            IDENTITY(1,1) PRIMARY KEY,
    UserId       INT            NOT NULL REFERENCES catalog.Users(UserId),
    Action       NVARCHAR(100)  NOT NULL,   -- 'PROVISION', 'PAUSE', 'DELETE', 'LOGIN'
    Detail       NVARCHAR(1000) NULL,
    CreatedAt    DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE catalog.LoginAudit (
    LoginId     INT            IDENTITY(1,1) PRIMARY KEY,
    UserId      INT            NOT NULL REFERENCES catalog.Users(UserId),
    Provider    NVARCHAR(50)   NULL,
    IpAddress   NVARCHAR(45)   NULL,
    CreatedAt   DATETIME2      NOT NULL DEFAULT SYSUTCDATETIME()
);

-- =============================================================
-- STORED PROCEDURES
-- =============================================================

-- Upsert user from OAuth provider
CREATE OR ALTER PROCEDURE catalog.sp_UpsertOAuthUser
    @Provider       NVARCHAR(50),
    @ProviderUserId NVARCHAR(255),
    @Email          NVARCHAR(255),
    @Name           NVARCHAR(255),
    @AvatarUrl      NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ProviderId INT = (SELECT ProviderId FROM catalog.Providers WHERE Name = @Provider);
    DECLARE @UserId INT;
    DECLARE @IsNewUser BIT = 0;

    -- Try existing user by provider
    SELECT @UserId = UserId FROM catalog.Users
    WHERE ProviderId = @ProviderId AND ProviderUserId = @ProviderUserId;

    IF @UserId IS NULL
    BEGIN
        -- New user
        INSERT INTO catalog.Users (Email, Name, AvatarUrl, ProviderId, ProviderUserId, LastLoginAt)
        VALUES (@Email, @Name, @AvatarUrl, @ProviderId, @ProviderUserId, SYSUTCDATETIME());

        SET @UserId = SCOPE_IDENTITY();
        SET @IsNewUser = 1;

        INSERT INTO catalog.ProvisioningAudit (UserId, Action, Detail)
        VALUES (@UserId, 'LOGIN', CONCAT('First login via ', @Provider));
    END
    ELSE
    BEGIN
        -- Update existing user info
        UPDATE catalog.Users
        SET Email = @Email,
            Name = @Name,
            AvatarUrl = COALESCE(@AvatarUrl, AvatarUrl),
            LastLoginAt = SYSUTCDATETIME()
        WHERE UserId = @UserId;
    END;

    INSERT INTO catalog.LoginAudit (UserId, Provider) VALUES (@UserId, @Provider);

    SELECT UserId = @UserId, Email = @Email, Name = @Name,
           AvatarUrl = @AvatarUrl, Provider = @Provider, IsNewUser = @IsNewUser;
END;
GO

-- Reserve a database slot for a user
CREATE OR ALTER PROCEDURE catalog.sp_ReserveDatabaseSlot
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Find an available cell (round-robin: pick the one with fewest tenants)
    DECLARE @CellId INT;
    SELECT TOP 1 @CellId = c.CellId
    FROM catalog.Cells c
    LEFT JOIN catalog.Tenants t ON c.CellId = t.CellId
    GROUP BY c.CellId
    ORDER BY COUNT(t.TenantId) ASC;

    IF @CellId IS NULL
        THROW 50000, 'No cells available. Contact the administrator.', 1;

    -- Generate unique names
    DECLARE @DbName NVARCHAR(255) = CONCAT('db_', @UserId, '_', FORMAT(SYSUTCDATETIME(), 'yyyyMMdd_HHmmss'));
    DECLARE @DbUser NVARCHAR(255) = CONCAT('user_', @UserId);
    DECLARE @Host NVARCHAR(255) = HOST_NAME();       -- MySQL host
    DECLARE @Port INT = 3306;

    SELECT DbName = @DbName, DbUser = @DbUser, Host = @Host, Port = @Port;
END;
GO

-- Confirm provisioning: record the database details
CREATE OR ALTER PROCEDURE catalog.sp_ConfirmProvisioning
    @UserId            INT,
    @DbName            NVARCHAR(255),
    @DbUser            NVARCHAR(255),
    @PasswordEncrypted NVARCHAR(500),
    @Host              NVARCHAR(255),
    @Port              INT,
    @Engine            NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    -- Pick the first cell (or assign based on @Host)
    DECLARE @CellId INT;
    SELECT TOP 1 @CellId = CellId FROM catalog.Cells ORDER BY CellId ASC;

    -- Create tenant record
    INSERT INTO catalog.Tenants (CellId, Slug, DbName, DbUser, PasswordEncrypted, Host, Port, Engine, UserId)
    VALUES (@CellId, CONCAT('tenant_', @UserId), @DbName, @DbUser, @PasswordEncrypted, @Host, @Port, @Engine, @UserId);

    DECLARE @TenantId INT = SCOPE_IDENTITY();

    -- Link user to tenant
    UPDATE catalog.Users SET TenantId = @TenantId WHERE UserId = @UserId;

    INSERT INTO catalog.ProvisioningAudit (UserId, Action, Detail)
    VALUES (@UserId, 'PROVISION', CONCAT('Database ', @DbName, ' provisioned on ', @Host, ':', @Port));
END;
GO

-- Get user's database connection info (password is encrypted, decrypt in app)
CREATE OR ALTER PROCEDURE catalog.sp_GetUserDatabase
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        Host = t.Host,
        Port = t.Port,
        DbName = t.DbName,
        DbUser = t.DbUser,
        Password = t.PasswordEncrypted,
        Engine = t.Engine,
        CreatedAt = t.CreatedAt,
        Status = CASE WHEN t.IsActive = 1 THEN 'Activa' ELSE 'Pausada' END
    FROM catalog.Tenants t
    WHERE t.UserId = @UserId
    ORDER BY t.CreatedAt DESC;
END;
GO

-- Get superadmin credentials by email
CREATE OR ALTER PROCEDURE catalog.sp_GetSuperAdminCredentials
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT UserId, Email, PasswordHash, TenantId, Role
    FROM catalog.Users
    WHERE Email = @Email AND Role = 'SuperAdmin';
END;
GO

-- Get user credentials by email (for email/password login)
CREATE OR ALTER PROCEDURE catalog.sp_GetUserCredentials
    @Email NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT UserId, Email, PasswordHash, TenantId, Role
    FROM catalog.Users
    WHERE Email = @Email AND Role = 'User';
END;
GO

-- Register a new user with email/password
CREATE OR ALTER PROCEDURE catalog.sp_RegisterUser
    @Email        NVARCHAR(255),
    @PasswordHash NVARCHAR(500),
    @TenantId     INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Check if email already exists
    IF EXISTS (SELECT 1 FROM catalog.Users WHERE Email = @Email)
        THROW 50000, 'A user with this email already exists.', 1;

    INSERT INTO catalog.Users (Email, Name, PasswordHash, TenantId)
    VALUES (@Email, @Email, @PasswordHash, @TenantId);

    SELECT UserId = SCOPE_IDENTITY();
END;
GO

-- Resolve cell+tenant slugs to connection components
CREATE OR ALTER PROCEDURE catalog.sp_ResolveTenantConnection
    @CellSlug  NVARCHAR(100),
    @TenantSlug NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        TenantId = t.TenantId,
        Host = t.Host,
        Port = t.Port,
        DbName = t.DbName,
        DbUser = t.DbUser,
        PasswordEncrypted = t.PasswordEncrypted,
        IsActive = t.IsActive
    FROM catalog.Tenants t
    INNER JOIN catalog.Cells c ON t.CellId = c.CellId
    WHERE c.Slug = @CellSlug AND t.Slug = @TenantSlug;
END;
GO

-- Create a new cell
CREATE OR ALTER PROCEDURE catalog.sp_CreateCell
    @Name NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Slug NVARCHAR(100) = LOWER(REPLACE(@Name, ' ', '-'));

    INSERT INTO catalog.Cells (Name, Slug) VALUES (@Name, @Slug);

    SELECT Id = SCOPE_IDENTITY(), Name = @Name, CreatedAt = SYSUTCDATETIME();
END;
GO

-- Create a new tenant
CREATE OR ALTER PROCEDURE catalog.sp_CreateTenant
    @CellId INT,
    @Slug   NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO catalog.Tenants (CellId, Slug, DbName, DbUser, Host)
    VALUES (@CellId, @Slug, CONCAT('db_', @Slug), CONCAT('user_', @Slug), HOST_NAME());

    SELECT Id = SCOPE_IDENTITY(), CellId = @CellId, Slug = @Slug,
           MaxSizeBytes = 20971520, CurrentSizeBytes = 0,
           CreatedAt = SYSUTCDATETIME(), LastActivityAt = NULL, IsActive = 1;
END;
GO

-- Pause a tenant
CREATE OR ALTER PROCEDURE catalog.sp_PauseTenant
    @TenantId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE catalog.Tenants SET IsActive = 0 WHERE TenantId = @TenantId;

    INSERT INTO catalog.ProvisioningAudit (UserId, Action, Detail)
    VALUES (COALESCE((SELECT UserId FROM catalog.Tenants WHERE TenantId = @TenantId), 0),
            'PAUSE', CONCAT('Tenant ', @TenantId, ' paused'));

    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- Delete a tenant
CREATE OR ALTER PROCEDURE catalog.sp_DeleteTenant
    @TenantId INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UserId INT = (SELECT UserId FROM catalog.Tenants WHERE TenantId = @TenantId);

    DELETE FROM catalog.Tenants WHERE TenantId = @TenantId;

    INSERT INTO catalog.ProvisioningAudit (UserId, Action, Detail)
    VALUES (COALESCE(@UserId, 0), 'DELETE', CONCAT('Tenant ', @TenantId, ' deleted'));

    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- List all tenants
CREATE OR ALTER PROCEDURE catalog.sp_ListTenants
AS
BEGIN
    SET NOCOUNT ON;

    SELECT t.TenantId AS Id, t.CellId, t.Slug, t.MaxSizeBytes, t.CurrentSizeBytes,
           t.CreatedAt, t.LastActivityAt, t.IsActive
    FROM catalog.Tenants t
    ORDER BY t.CreatedAt DESC;
END;
GO

-- =============================================================
-- VIEWS
-- =============================================================

CREATE OR ALTER VIEW catalog.vw_UserDashboard
AS
SELECT
    u.UserId,
    Status = CASE WHEN t.IsActive = 1 THEN 'Activa' ELSE 'Pausada' END,
    UsedBytes = COALESCE(t.CurrentSizeBytes, 0),
    MaxBytes = COALESCE(t.MaxSizeBytes, 20971520),
    CreatedAt = COALESCE(t.CreatedAt, u.CreatedAt),
    LastActivity = COALESCE(t.LastActivityAt, u.LastLoginAt, u.CreatedAt)
FROM catalog.Users u
LEFT JOIN catalog.Tenants t ON u.UserId = t.UserId AND t.IsActive = 1;
GO

CREATE OR ALTER VIEW catalog.vw_LandingMetrics
AS
SELECT
    TotalUsers     = (SELECT COUNT(*) FROM catalog.Users),
    TotalDatabases = (SELECT COUNT(*) FROM catalog.Tenants),
    ActiveDatabases = (SELECT COUNT(*) FROM catalog.Tenants WHERE IsActive = 1),
    TotalLogins     = (SELECT COUNT(*) FROM catalog.LoginAudit),
    ActiveUsers     = (SELECT COUNT(DISTINCT UserId) FROM catalog.LoginAudit WHERE CreatedAt >= DATEADD(DAY, -7, SYSUTCDATETIME())),
    Availability    = '99.98%';
GO

-- =============================================================
-- SEED DATA
-- =============================================================

-- Create a default cell if none exists
IF NOT EXISTS (SELECT 1 FROM catalog.Cells)
BEGIN
    INSERT INTO catalog.Cells (Name, Slug) VALUES ('alpha', 'alpha');
END;
GO

-- Create a default superadmin (password: change on first login)
IF NOT EXISTS (SELECT 1 FROM catalog.Users WHERE Role = 'SuperAdmin')
BEGIN
    INSERT INTO catalog.Users (Email, Name, PasswordHash, Role)
    VALUES ('admin@celula-cap-theorem.io', 'SuperAdmin',
            '$2a$11$K4YfGqJ1e4YHIpQGd4FbIOlHQg5J0XeF9pLq8Xm7t1uR5n3s2VvWa',  -- BCrypt hash of 'Admin123!'
            'SuperAdmin');
END;
GO
