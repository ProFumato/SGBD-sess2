USE [PadelCourtManagement];
GO

-- Application role: normal runtime DML only; it does not receive schema modification or db_owner rights.
IF NOT EXISTS
(
    SELECT 1
    FROM sys.database_principals
    WHERE [name] = N'pcm_app_runtime'
      AND [type] = 'R'
)
BEGIN
    CREATE ROLE [pcm_app_runtime];
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.database_principals
    WHERE [name] = N'pcm_schema_deployer'
      AND [type] = 'R'
)
BEGIN
    CREATE ROLE [pcm_schema_deployer];
END;
GO

GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::[pcm] TO [pcm_app_runtime];
-- This project uses direct parameterized table access from the repositories rather than stored procedures only.
GRANT EXECUTE ON SCHEMA::[pcm] TO [pcm_app_runtime];
GO

-- Separate deployment role for schema changes, so the application login does not need DDL permissions.
GRANT ALTER ON SCHEMA::[pcm] TO [pcm_schema_deployer];
GRANT CREATE TABLE TO [pcm_schema_deployer];
GRANT CREATE PROCEDURE TO [pcm_schema_deployer];
GRANT CREATE FUNCTION TO [pcm_schema_deployer];
GRANT CREATE VIEW TO [pcm_schema_deployer];
GO


-- These database users expect their server-level logins to already exist.
IF NOT EXISTS
(
    SELECT 1
    FROM sys.database_principals
    WHERE [name] = N'PadelCourtAppLogin'
)
BEGIN
    CREATE USER [PadelCourtAppLogin] FOR LOGIN [PadelCourtAppLogin];
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.database_principals
    WHERE [name] = N'PadelCourtSchemaLogin'
)
BEGIN
    CREATE USER [PadelCourtSchemaLogin] FOR LOGIN [PadelCourtSchemaLogin];
END;
GO

ALTER ROLE [pcm_app_runtime] ADD MEMBER [PadelCourtAppLogin];
ALTER ROLE [pcm_schema_deployer] ADD MEMBER [PadelCourtSchemaLogin];
GO
