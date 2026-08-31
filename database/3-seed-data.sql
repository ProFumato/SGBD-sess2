USE [PadelCourtManagement];
GO

IF NOT EXISTS
(
    SELECT 1
    FROM [pcm].[Member]
    WHERE [Matricule] = 'G0001'
)
BEGIN
    INSERT INTO [pcm].[Member]
    (
        [Matricule],
        [DisplayName],
        [MembershipCategory],
        [HomeSiteId]
    )
    VALUES
    (
        'G0001',
        N'Initial Global Administrator',
        'G',
        NULL
    );
END;
GO

DECLARE @InitialAdministratorMemberId INT =
(
    SELECT [MemberId]
    FROM [pcm].[Member]
    WHERE [Matricule] = 'G0001'
);

IF NOT EXISTS
(
    SELECT 1
    FROM [pcm].[AdministratorAssignment]
    WHERE [MemberId] = @InitialAdministratorMemberId
)
BEGIN
    INSERT INTO [pcm].[AdministratorAssignment]
    (
        [MemberId],
        [Scope],
        [SiteId]
    )
    VALUES
    (
        @InitialAdministratorMemberId,
        'G',
        NULL
    );
END;
GO
