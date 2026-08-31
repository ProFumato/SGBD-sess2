USE [PadelCourtManagement];
GO

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

BEGIN TRY
    IF (SELECT COUNT(*) FROM [pcm].[Member] WHERE [Matricule] = 'G0001') <> 1
        THROW 52000, 'The seeded global administrator is missing.', 1;

    IF (SELECT COUNT(*) FROM sys.tables WHERE [schema_id] = SCHEMA_ID(N'pcm')) <> 12
        THROW 52001, 'The expected pcm schema tables are missing.', 1;

    IF OBJECT_ID(N'pcm.TR_AdministratorAssignment_RequireGlobalAdministrator', N'TR') IS NULL
       OR OBJECT_ID(N'pcm.TR_Member_RequireGlobalAdministrator', N'TR') IS NULL
       OR OBJECT_ID(N'pcm.TR_SiteAnnualSchedule_ValidateExistingMatches', N'TR') IS NULL
    BEGIN
        THROW 52009, 'The administration integrity triggers are missing.', 1;
    END;

    INSERT INTO [pcm].[Site] ([Name])
    VALUES (N'Database integration validation site');

    DECLARE @SiteId INT = SCOPE_IDENTITY();

    BEGIN TRY
        INSERT INTO [pcm].[Member] ([Matricule], [DisplayName], [MembershipCategory], [HomeSiteId])
        VALUES ('G0002', N'Invalid global member', 'G', @SiteId);
        THROW 52002, 'Global member with a home site was accepted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 547
        BEGIN
            THROW;
        END;
    END CATCH;

    INSERT INTO [pcm].[Member] ([Matricule], [DisplayName], [MembershipCategory], [HomeSiteId])
    VALUES
        ('L00001', N'Local organizer', 'L', NULL),
        ('S00001', N'Site member one', 'S', @SiteId),
        ('S00002', N'Site member two', 'S', @SiteId),
        ('S00003', N'Site member three', 'S', @SiteId),
        ('S00004', N'Site member four', 'S', @SiteId),
        ('S00005', N'Site member five', 'S', @SiteId);

    DECLARE @OrganizerId INT =
    (
        SELECT [MemberId] FROM [pcm].[Member] WHERE [Matricule] = 'L00001'
    );

    INSERT INTO [pcm].[Court] ([SiteId], [Name])
    VALUES (@SiteId, N'Integration court');

    DECLARE @CourtId INT = SCOPE_IDENTITY();

    BEGIN TRY
        INSERT INTO [pcm].[Match] ([CourtId], [OrganizerMemberId], [StartsAt], [EndsAt], [Visibility])
        VALUES (@CourtId, @OrganizerId, '2030-01-10T08:00:00', '2030-01-10T09:00:00', 'Public');
        THROW 52003, 'Invalid match duration was accepted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 547
        BEGIN
            THROW;
        END;
    END CATCH;

    INSERT INTO [pcm].[Match] ([CourtId], [OrganizerMemberId], [StartsAt], [EndsAt], [Visibility])
    VALUES (@CourtId, @OrganizerId, '2030-01-10T10:00:00', '2030-01-10T11:30:00', 'Public');

    DECLARE @MatchId INT = SCOPE_IDENTITY();

    INSERT INTO [pcm].[SiteAnnualSchedule]
    (
        [SiteId],
        [CalendarYear],
        [OpeningTime],
        [ClosingTime]
    )
    VALUES (@SiteId, 2030, '09:00:00', '22:00:00');

    BEGIN TRY
        UPDATE [pcm].[SiteAnnualSchedule]
        SET [OpeningTime] = '10:30:00'
        WHERE [SiteId] = @SiteId
          AND [CalendarYear] = 2030;
        THROW 52010, 'Schedule excluding an existing match was accepted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 51007
        BEGIN
            THROW;
        END;
    END CATCH;

    BEGIN TRY
        INSERT INTO [pcm].[Match] ([CourtId], [OrganizerMemberId], [StartsAt], [EndsAt], [Visibility])
        VALUES (@CourtId, @OrganizerId, '2030-01-10T11:40:00', '2030-01-10T13:10:00', 'Public');
        THROW 52004, 'Match without the required 15-minute gap was accepted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 51000
        BEGIN
            THROW;
        END;
    END CATCH;

    BEGIN TRY
        INSERT INTO [pcm].[Closure] ([Scope], [SiteId], [StartsAt], [EndsAt], [Reason])
        VALUES ('S', @SiteId, '2030-01-10T10:30:00', '2030-01-10T11:00:00', N'Integration closure');
        THROW 52005, 'Closure overlapping an existing match was accepted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 51002
        BEGIN
            THROW;
        END;
    END CATCH;

    INSERT INTO [pcm].[MatchParticipant] ([MatchId], [MemberId], [IsOrganizer])
    SELECT
        @MatchId,
        [MemberId],
        CASE WHEN [Matricule] = 'S00001' THEN 1 ELSE 0 END
    FROM [pcm].[Member]
    WHERE [Matricule] IN ('S00001', 'S00002', 'S00003', 'S00004');

    BEGIN TRY
        INSERT INTO [pcm].[MatchParticipant] ([MatchId], [MemberId])
        SELECT @MatchId, [MemberId]
        FROM [pcm].[Member]
        WHERE [Matricule] = 'S00005';
        THROW 52006, 'Fifth active participant was accepted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 51003
        BEGIN
            THROW;
        END;
    END CATCH;

    DECLARE @ParticipantId INT =
    (
        SELECT TOP (1) [MatchParticipantId]
        FROM [pcm].[MatchParticipant]
        WHERE [MatchId] = @MatchId
    );

    INSERT INTO [pcm].[Payment] ([PayerMemberId], [Amount], [PaymentStatus], [PaidAt])
    VALUES (@OrganizerId, 60.00, 'Failed', NULL);

    DECLARE @FailedPaymentId INT = SCOPE_IDENTITY();

    BEGIN TRY
        INSERT INTO [pcm].[PaymentAllocation] ([PaymentId], [MatchParticipantId], [Amount])
        VALUES (@FailedPaymentId, @ParticipantId, 10.00);
        THROW 52007, 'Allocation for a failed payment was accepted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 51004
        BEGIN
            THROW;
        END;
    END CATCH;

    INSERT INTO [pcm].[Payment] ([PayerMemberId], [Amount], [PaymentStatus], [PaidAt])
    VALUES (@OrganizerId, 60.00, 'Succeeded', SYSUTCDATETIME());

    DECLARE @SucceededPaymentId INT = SCOPE_IDENTITY();

    INSERT INTO [pcm].[PaymentAllocation] ([PaymentId], [MatchParticipantId], [Amount])
    VALUES (@SucceededPaymentId, @ParticipantId, 40.00);

    BEGIN TRY
        INSERT INTO [pcm].[PaymentAllocation] ([PaymentId], [MatchParticipantId], [Amount])
        VALUES (@SucceededPaymentId, @ParticipantId, 21.00);
        THROW 52008, 'Payment over-allocation was accepted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 51005
        BEGIN
            THROW;
        END;
    END CATCH;

    DELETE [pcm].[PaymentAllocation]
    WHERE [PaymentId] IN (SELECT [PaymentId] FROM [pcm].[Payment] WHERE [PayerMemberId] = @OrganizerId);
    DELETE [pcm].[Payment] WHERE [PayerMemberId] = @OrganizerId;
    DELETE [pcm].[MatchParticipant] WHERE [MatchId] = @MatchId;
    DELETE [pcm].[Match] WHERE [MatchId] = @MatchId;
    DELETE [pcm].[SiteAnnualSchedule] WHERE [SiteId] = @SiteId;
    DELETE [pcm].[Court] WHERE [CourtId] = @CourtId;
    DELETE [pcm].[Member] WHERE [Matricule] IN ('L00001', 'S00001', 'S00002', 'S00003', 'S00004', 'S00005');
    DELETE [pcm].[Site] WHERE [SiteId] = @SiteId;

    SELECT
        'PASS' AS [Result],
        'Schema, seed data, booking, participant, closure, and payment constraints verified.' AS [Details],
        SYSUTCDATETIME() AS [VerifiedAtUtc];
END TRY
BEGIN CATCH
    THROW;
END CATCH;
