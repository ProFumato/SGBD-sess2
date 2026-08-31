USE [master];
GO

IF DB_ID(N'PadelCourtManagement') IS NULL
BEGIN
    CREATE DATABASE [PadelCourtManagement];
END;
GO
