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

IF SCHEMA_ID(N'pcm') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA [pcm]');
END;
GO

IF OBJECT_ID(N'pcm.Site', N'U') IS NULL
BEGIN
    CREATE TABLE [pcm].[Site]
    (
        [SiteId] INT IDENTITY(1, 1) NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [CreatedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_Site_CreatedAt] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_Site] PRIMARY KEY ([SiteId]),
        CONSTRAINT [UQ_Site_Name] UNIQUE ([Name])
    );
END;
GO

IF OBJECT_ID(N'pcm.Member', N'U') IS NULL
BEGIN
    CREATE TABLE [pcm].[Member]
    (
        [MemberId] INT IDENTITY(1, 1) NOT NULL,
        [Matricule] VARCHAR(6) NOT NULL,
        [DisplayName] NVARCHAR(120) NOT NULL,
        [MembershipCategory] CHAR(1) NOT NULL,
        [HomeSiteId] INT NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_Member_IsActive] DEFAULT 1,
        [CreatedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_Member_CreatedAt] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_Member] PRIMARY KEY ([MemberId]),
        CONSTRAINT [UQ_Member_Matricule] UNIQUE ([Matricule]),
        CONSTRAINT [FK_Member_HomeSite] FOREIGN KEY ([HomeSiteId]) REFERENCES [pcm].[Site] ([SiteId]),
        CONSTRAINT [CK_Member_CategoryAndMatricule] CHECK
        (
            ([MembershipCategory] = 'G'
                AND [Matricule] LIKE 'G[0-9][0-9][0-9][0-9]'
                AND DATALENGTH([Matricule]) = 5
                AND [HomeSiteId] IS NULL)
            OR
            ([MembershipCategory] IN ('S', 'L')
                AND [Matricule] LIKE '[SL][0-9][0-9][0-9][0-9][0-9]'
                AND DATALENGTH([Matricule]) = 6
                AND
                (
                    ([MembershipCategory] = 'S' AND [HomeSiteId] IS NOT NULL)
                    OR ([MembershipCategory] = 'L' AND [HomeSiteId] IS NULL)
                ))
        )
    );
END;
GO

IF OBJECT_ID(N'pcm.AdministratorAssignment', N'U') IS NULL
BEGIN
    CREATE TABLE [pcm].[AdministratorAssignment]
    (
        [AdministratorAssignmentId] INT IDENTITY(1, 1) NOT NULL,
        [MemberId] INT NOT NULL,
        [Scope] CHAR(1) NOT NULL,
        [SiteId] INT NULL,
        CONSTRAINT [PK_AdministratorAssignment] PRIMARY KEY ([AdministratorAssignmentId]),
        CONSTRAINT [UQ_AdministratorAssignment_Member] UNIQUE ([MemberId]),
        CONSTRAINT [FK_AdministratorAssignment_Member] FOREIGN KEY ([MemberId]) REFERENCES [pcm].[Member] ([MemberId]),
        CONSTRAINT [FK_AdministratorAssignment_Site] FOREIGN KEY ([SiteId]) REFERENCES [pcm].[Site] ([SiteId]),
        CONSTRAINT [CK_AdministratorAssignment_Scope] CHECK
        (
            ([Scope] = 'G' AND [SiteId] IS NULL)
            OR ([Scope] = 'S' AND [SiteId] IS NOT NULL)
        )
    );
END;
GO

IF OBJECT_ID(N'pcm.Court', N'U') IS NULL
BEGIN
    CREATE TABLE [pcm].[Court]
    (
        [CourtId] INT IDENTITY(1, 1) NOT NULL,
        [SiteId] INT NOT NULL,
        [Name] NVARCHAR(100) NOT NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_Court_IsActive] DEFAULT 1,
        CONSTRAINT [PK_Court] PRIMARY KEY ([CourtId]),
        CONSTRAINT [FK_Court_Site] FOREIGN KEY ([SiteId]) REFERENCES [pcm].[Site] ([SiteId]),
        CONSTRAINT [UQ_Court_Site_Name] UNIQUE ([SiteId], [Name])
    );
END;
GO

IF OBJECT_ID(N'pcm.SiteAnnualSchedule', N'U') IS NULL
BEGIN
    CREATE TABLE [pcm].[SiteAnnualSchedule]
    (
        [SiteAnnualScheduleId] INT IDENTITY(1, 1) NOT NULL,
        [SiteId] INT NOT NULL,
        [CalendarYear] SMALLINT NOT NULL,
        [OpeningTime] TIME(0) NOT NULL,
        [ClosingTime] TIME(0) NOT NULL,
        CONSTRAINT [PK_SiteAnnualSchedule] PRIMARY KEY ([SiteAnnualScheduleId]),
        CONSTRAINT [FK_SiteAnnualSchedule_Site] FOREIGN KEY ([SiteId]) REFERENCES [pcm].[Site] ([SiteId]),
        CONSTRAINT [UQ_SiteAnnualSchedule_Site_Year] UNIQUE ([SiteId], [CalendarYear]),
        CONSTRAINT [CK_SiteAnnualSchedule_Year] CHECK ([CalendarYear] BETWEEN 2000 AND 9999),
        CONSTRAINT [CK_SiteAnnualSchedule_Times] CHECK ([OpeningTime] < [ClosingTime])
    );
END;
GO

IF OBJECT_ID(N'pcm.Closure', N'U') IS NULL
BEGIN
    CREATE TABLE [pcm].[Closure]
    (
        [ClosureId] INT IDENTITY(1, 1) NOT NULL,
        [Scope] CHAR(1) NOT NULL,
        [SiteId] INT NULL,
        [StartsAt] DATETIME2(0) NOT NULL,
        [EndsAt] DATETIME2(0) NOT NULL,
        [Reason] NVARCHAR(250) NOT NULL,
        CONSTRAINT [PK_Closure] PRIMARY KEY ([ClosureId]),
        CONSTRAINT [FK_Closure_Site] FOREIGN KEY ([SiteId]) REFERENCES [pcm].[Site] ([SiteId]),
        CONSTRAINT [CK_Closure_Scope] CHECK
        (
            ([Scope] = 'G' AND [SiteId] IS NULL)
            OR ([Scope] = 'S' AND [SiteId] IS NOT NULL)
        ),
        CONSTRAINT [CK_Closure_Dates] CHECK ([StartsAt] < [EndsAt])
    );
END;
GO

IF OBJECT_ID(N'pcm.Match', N'U') IS NULL
BEGIN
    CREATE TABLE [pcm].[Match]
    (
        [MatchId] INT IDENTITY(1, 1) NOT NULL,
        [CourtId] INT NOT NULL,
        [OrganizerMemberId] INT NOT NULL,
        [StartsAt] DATETIME2(0) NOT NULL,
        [EndsAt] DATETIME2(0) NOT NULL,
        [Visibility] VARCHAR(7) NOT NULL,
        [Price] DECIMAL(9, 2) NOT NULL CONSTRAINT [DF_Match_Price] DEFAULT 60.00,
        [CreatedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_Match_CreatedAt] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_Match] PRIMARY KEY ([MatchId]),
        CONSTRAINT [FK_Match_Court] FOREIGN KEY ([CourtId]) REFERENCES [pcm].[Court] ([CourtId]),
        CONSTRAINT [FK_Match_Organizer] FOREIGN KEY ([OrganizerMemberId]) REFERENCES [pcm].[Member] ([MemberId]),
        CONSTRAINT [CK_Match_Duration] CHECK ([EndsAt] = DATEADD(MINUTE, 90, [StartsAt])),
        CONSTRAINT [CK_Match_Visibility] CHECK ([Visibility] IN ('Private', 'Public')),
        CONSTRAINT [CK_Match_Price] CHECK ([Price] = 60.00)
    );
END;
GO

IF OBJECT_ID(N'pcm.MatchParticipant', N'U') IS NULL
BEGIN
    CREATE TABLE [pcm].[MatchParticipant]
    (
        [MatchParticipantId] INT IDENTITY(1, 1) NOT NULL,
        [MatchId] INT NOT NULL,
        [MemberId] INT NOT NULL,
        [IsOrganizer] BIT NOT NULL CONSTRAINT [DF_MatchParticipant_IsOrganizer] DEFAULT 0,
        [ParticipationStatus] VARCHAR(9) NOT NULL CONSTRAINT [DF_MatchParticipant_Status] DEFAULT 'Pending',
        [AddedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_MatchParticipant_AddedAt] DEFAULT SYSUTCDATETIME(),
        CONSTRAINT [PK_MatchParticipant] PRIMARY KEY ([MatchParticipantId]),
        CONSTRAINT [FK_MatchParticipant_Match] FOREIGN KEY ([MatchId]) REFERENCES [pcm].[Match] ([MatchId]),
        CONSTRAINT [FK_MatchParticipant_Member] FOREIGN KEY ([MemberId]) REFERENCES [pcm].[Member] ([MemberId]),
        CONSTRAINT [UQ_MatchParticipant_Match_Member] UNIQUE ([MatchId], [MemberId]),
        CONSTRAINT [CK_MatchParticipant_Status] CHECK ([ParticipationStatus] IN ('Pending', 'Confirmed', 'Removed'))
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'UX_MatchParticipant_Organizer'
      AND [object_id] = OBJECT_ID(N'pcm.MatchParticipant')
)
BEGIN
    CREATE UNIQUE INDEX [UX_MatchParticipant_Organizer]
        ON [pcm].[MatchParticipant] ([MatchId])
        WHERE [IsOrganizer] = 1;
END;
GO

IF OBJECT_ID(N'pcm.Payment', N'U') IS NULL
BEGIN
    CREATE TABLE [pcm].[Payment]
    (
        [PaymentId] INT IDENTITY(1, 1) NOT NULL,
        [PayerMemberId] INT NOT NULL,
        [Amount] DECIMAL(9, 2) NOT NULL,
        [PaymentStatus] VARCHAR(9) NOT NULL,
        [PaidAt] DATETIME2(0) NULL,
        CONSTRAINT [PK_Payment] PRIMARY KEY ([PaymentId]),
        CONSTRAINT [FK_Payment_Payer] FOREIGN KEY ([PayerMemberId]) REFERENCES [pcm].[Member] ([MemberId]),
        CONSTRAINT [CK_Payment_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [CK_Payment_Status] CHECK
        (
            ([PaymentStatus] = 'Succeeded' AND [PaidAt] IS NOT NULL)
            OR ([PaymentStatus] = 'Failed' AND [PaidAt] IS NULL)
        )
    );
END;
GO

IF OBJECT_ID(N'pcm.Debt', N'U') IS NULL
BEGIN
    CREATE TABLE [pcm].[Debt]
    (
        [DebtId] INT IDENTITY(1, 1) NOT NULL,
        [OrganizerMemberId] INT NOT NULL,
        [MatchId] INT NOT NULL,
        [InitialAmount] DECIMAL(9, 2) NOT NULL,
        [OutstandingAmount] DECIMAL(9, 2) NOT NULL,
        [SettledAt] DATETIME2(0) NULL,
        CONSTRAINT [PK_Debt] PRIMARY KEY ([DebtId]),
        CONSTRAINT [UQ_Debt_Match] UNIQUE ([MatchId]),
        CONSTRAINT [FK_Debt_Organizer] FOREIGN KEY ([OrganizerMemberId]) REFERENCES [pcm].[Member] ([MemberId]),
        CONSTRAINT [FK_Debt_Match] FOREIGN KEY ([MatchId]) REFERENCES [pcm].[Match] ([MatchId]),
        CONSTRAINT [CK_Debt_Amounts] CHECK
        (
            [InitialAmount] > 0
            AND [OutstandingAmount] >= 0
            AND [OutstandingAmount] <= [InitialAmount]
        ),
        CONSTRAINT [CK_Debt_Settlement] CHECK
        (
            ([OutstandingAmount] = 0 AND [SettledAt] IS NOT NULL)
            OR ([OutstandingAmount] > 0 AND [SettledAt] IS NULL)
        )
    );
END;
GO

IF OBJECT_ID(N'pcm.PaymentAllocation', N'U') IS NULL
BEGIN
    CREATE TABLE [pcm].[PaymentAllocation]
    (
        [PaymentAllocationId] INT IDENTITY(1, 1) NOT NULL,
        [PaymentId] INT NOT NULL,
        [MatchParticipantId] INT NULL,
        [DebtId] INT NULL,
        [Amount] DECIMAL(9, 2) NOT NULL,
        CONSTRAINT [PK_PaymentAllocation] PRIMARY KEY ([PaymentAllocationId]),
        CONSTRAINT [FK_PaymentAllocation_Payment] FOREIGN KEY ([PaymentId]) REFERENCES [pcm].[Payment] ([PaymentId]),
        CONSTRAINT [FK_PaymentAllocation_MatchParticipant] FOREIGN KEY ([MatchParticipantId]) REFERENCES [pcm].[MatchParticipant] ([MatchParticipantId]),
        CONSTRAINT [FK_PaymentAllocation_Debt] FOREIGN KEY ([DebtId]) REFERENCES [pcm].[Debt] ([DebtId]),
        CONSTRAINT [CK_PaymentAllocation_Target] CHECK ([MatchParticipantId] IS NOT NULL OR [DebtId] IS NOT NULL),
        CONSTRAINT [CK_PaymentAllocation_Amount] CHECK ([Amount] > 0)
    );
END;
GO

IF OBJECT_ID(N'pcm.BookingBan', N'U') IS NULL
BEGIN
    CREATE TABLE [pcm].[BookingBan]
    (
        [BookingBanId] INT IDENTITY(1, 1) NOT NULL,
        [MemberId] INT NOT NULL,
        [SourceMatchId] INT NOT NULL,
        [StartsAt] DATETIME2(0) NOT NULL,
        [EndsAt] DATETIME2(0) NOT NULL,
        [Reason] NVARCHAR(250) NOT NULL,
        CONSTRAINT [PK_BookingBan] PRIMARY KEY ([BookingBanId]),
        CONSTRAINT [FK_BookingBan_Member] FOREIGN KEY ([MemberId]) REFERENCES [pcm].[Member] ([MemberId]),
        CONSTRAINT [FK_BookingBan_SourceMatch] FOREIGN KEY ([SourceMatchId]) REFERENCES [pcm].[Match] ([MatchId]),
        CONSTRAINT [CK_BookingBan_Dates] CHECK ([StartsAt] < [EndsAt])
    );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Match_Court_StartsAt'
      AND [object_id] = OBJECT_ID(N'pcm.Match')
)
BEGIN
    CREATE INDEX [IX_Match_Court_StartsAt]
        ON [pcm].[Match] ([CourtId], [StartsAt]);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Closure_Scope_Site_Dates'
      AND [object_id] = OBJECT_ID(N'pcm.Closure')
)
BEGIN
    CREATE INDEX [IX_Closure_Scope_Site_Dates]
        ON [pcm].[Closure] ([Scope], [SiteId], [StartsAt], [EndsAt]);
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE [name] = N'IX_Debt_Organizer_Outstanding'
      AND [object_id] = OBJECT_ID(N'pcm.Debt')
)
BEGIN
    CREATE INDEX [IX_Debt_Organizer_Outstanding]
        ON [pcm].[Debt] ([OrganizerMemberId], [OutstandingAmount]);
END;
GO

CREATE OR ALTER TRIGGER [pcm].[TR_Match_ValidateAvailability]
ON [pcm].[Match]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS [NewMatch]
        INNER JOIN [pcm].[Match] AS [ExistingMatch] WITH (UPDLOCK, HOLDLOCK)
            ON [ExistingMatch].[CourtId] = [NewMatch].[CourtId]
            AND [ExistingMatch].[MatchId] <> [NewMatch].[MatchId]
            AND [NewMatch].[StartsAt] < DATEADD(MINUTE, 15, [ExistingMatch].[EndsAt])
            AND [ExistingMatch].[StartsAt] < DATEADD(MINUTE, 15, [NewMatch].[EndsAt])
    )
    BEGIN
        THROW 51000, 'A court requires a 15-minute gap between matches.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS [NewMatch]
        INNER JOIN [pcm].[Court] AS [Court]
            ON [Court].[CourtId] = [NewMatch].[CourtId]
        INNER JOIN [pcm].[Closure] AS [Closure] WITH (UPDLOCK, HOLDLOCK)
            ON ([Closure].[Scope] = 'G' OR [Closure].[SiteId] = [Court].[SiteId])
            AND [NewMatch].[StartsAt] < [Closure].[EndsAt]
            AND [Closure].[StartsAt] < [NewMatch].[EndsAt]
    )
    BEGIN
        THROW 51001, 'A match cannot overlap a global or site closure.', 1;
    END;
END;
GO

CREATE OR ALTER TRIGGER [pcm].[TR_Closure_RejectReservedPeriod]
ON [pcm].[Closure]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS [NewClosure]
        INNER JOIN [pcm].[Court] AS [Court]
            ON [NewClosure].[Scope] = 'G'
            OR [NewClosure].[SiteId] = [Court].[SiteId]
        INNER JOIN [pcm].[Match] AS [Match] WITH (UPDLOCK, HOLDLOCK)
            ON [Match].[CourtId] = [Court].[CourtId]
            AND [Match].[StartsAt] < [NewClosure].[EndsAt]
            AND [NewClosure].[StartsAt] < [Match].[EndsAt]
    )
    BEGIN
        THROW 51002, 'A closure cannot overlap an existing match.', 1;
    END;
END;
GO

CREATE OR ALTER TRIGGER [pcm].[TR_MatchParticipant_EnforceCapacity]
ON [pcm].[MatchParticipant]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM [pcm].[MatchParticipant] AS [Participant] WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN
        (
            SELECT DISTINCT [MatchId]
            FROM inserted
        ) AS [ChangedMatch]
            ON [ChangedMatch].[MatchId] = [Participant].[MatchId]
        WHERE [Participant].[ParticipationStatus] <> 'Removed'
        GROUP BY [Participant].[MatchId]
        HAVING COUNT(*) > 4
    )
    BEGIN
        THROW 51003, 'A match cannot have more than four active participants.', 1;
    END;
END;
GO

CREATE OR ALTER TRIGGER [pcm].[TR_PaymentAllocation_ValidateAmount]
ON [pcm].[PaymentAllocation]
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM
        (
            SELECT [PaymentId] FROM inserted
            UNION
            SELECT [PaymentId] FROM deleted
        ) AS [ChangedPayment]
        INNER JOIN [pcm].[Payment] AS [Payment]
            ON [Payment].[PaymentId] = [ChangedPayment].[PaymentId]
        WHERE [Payment].[PaymentStatus] <> 'Succeeded'
    )
    BEGIN
        THROW 51004, 'Payment allocations require a successful payment.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM
        (
            SELECT [PaymentId] FROM inserted
            UNION
            SELECT [PaymentId] FROM deleted
        ) AS [Changed]
        INNER JOIN [pcm].[Payment] AS [Payment]
            ON [Payment].[PaymentId] = [Changed].[PaymentId]
        LEFT JOIN [pcm].[PaymentAllocation] AS [Allocation]
            ON [Allocation].[PaymentId] = [Payment].[PaymentId]
        GROUP BY [Payment].[PaymentId], [Payment].[Amount]
        HAVING COALESCE(SUM([Allocation].[Amount]), 0) > [Payment].[Amount]
    )
    BEGIN
        THROW 51005, 'Payment allocations cannot exceed the payment amount.', 1;
    END;
END;
GO
