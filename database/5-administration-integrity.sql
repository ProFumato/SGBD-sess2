USE [PadelCourtManagement];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
GO

CREATE OR ALTER TRIGGER [pcm].[TR_AdministratorAssignment_RequireGlobalAdministrator]
ON [pcm].[AdministratorAssignment]
AFTER UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS
    (
        SELECT 1
        FROM [pcm].[AdministratorAssignment] AS [Assignment] WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN [pcm].[Member] AS [Member]
            ON [Member].[MemberId] = [Assignment].[MemberId]
        WHERE [Assignment].[Scope] = 'G'
          AND [Member].[IsActive] = 1
    )
    BEGIN
        ;THROW 51006, 'At least one active global administrator must remain assigned.', 1;
    END;
END;
GO

CREATE OR ALTER TRIGGER [pcm].[TR_Member_RequireGlobalAdministrator]
ON [pcm].[Member]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF UPDATE([IsActive])
       AND NOT EXISTS
       (
           SELECT 1
           FROM [pcm].[AdministratorAssignment] AS [Assignment] WITH (UPDLOCK, HOLDLOCK)
           INNER JOIN [pcm].[Member] AS [Member]
               ON [Member].[MemberId] = [Assignment].[MemberId]
           WHERE [Assignment].[Scope] = 'G'
             AND [Member].[IsActive] = 1
       )
    BEGIN
        ;THROW 51006, 'At least one active global administrator must remain assigned.', 1;
    END;
END;
GO

CREATE OR ALTER TRIGGER [pcm].[TR_SiteAnnualSchedule_ValidateExistingMatches]
ON [pcm].[SiteAnnualSchedule]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS [Schedule]
        INNER JOIN [pcm].[Court] AS [Court]
            ON [Court].[SiteId] = [Schedule].[SiteId]
        INNER JOIN [pcm].[Match] AS [Match] WITH (UPDLOCK, HOLDLOCK)
            ON [Match].[CourtId] = [Court].[CourtId]
            AND DATEPART(YEAR, [Match].[StartsAt]) = [Schedule].[CalendarYear]
            AND
            (
                CONVERT(TIME(0), [Match].[StartsAt]) < [Schedule].[OpeningTime]
                OR CONVERT(TIME(0), [Match].[EndsAt]) > [Schedule].[ClosingTime]
            )
    )
    BEGIN
        ;THROW 51007, 'A schedule cannot exclude an existing match.', 1;
    END;
END;
GO
