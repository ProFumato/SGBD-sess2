using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PadelCourtManagement.Application;
using PadelCourtManagement.Application.Administration;
using PadelCourtManagement.Domain;
using PadelCourtManagement.Infrastructure;

namespace PadelCourtManagement.Tests;

public sealed class SqlServerIntegrationTests
{
    [Fact]
    public async Task Reservation_is_persisted_with_organizer_participant()
    {
        var database = await IntegrationDatabase.CreateAsync();

        try
        {
            var now = DateTime.Now;
            var request = new ReservationRequest(
                database.MemberMatricule,
                database.CourtId,
                DateOnly.FromDateTime(now).AddDays(3),
                new TimeOnly(10, 0),
                ReservationVisibility.Private);

            var result = await new AvailabilityService(new SqlAvailabilityRepository(database.Configuration))
                .CreateReservationAsync(request, CancellationToken.None);

            Assert.Equal(database.CourtId, result.CourtId);
            Assert.Equal(ReservationVisibility.Private, result.Visibility);

            var persisted = await database.QuerySingleAsync(
                """
                SELECT m.Visibility, p.MemberId, p.IsOrganizer, p.ParticipationStatus
                FROM pcm.Match AS m
                INNER JOIN pcm.MatchParticipant AS p ON p.MatchId = m.MatchId
                WHERE m.MatchId = @MatchId;
                """,
                command => command.Parameters.Add("@MatchId", SqlDbType.Int).Value = result.MatchId);

            Assert.Equal("Private", persisted[0]);
            Assert.Equal(database.MemberId, persisted[1]);
            Assert.True((bool)persisted[2]);
            Assert.Equal("Pending", persisted[3]);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [Fact]
    public async Task Overlapping_reservation_is_rejected_by_sql_transaction()
    {
        var database = await IntegrationDatabase.CreateAsync();

        try
        {
            var service = new AvailabilityService(new SqlAvailabilityRepository(database.Configuration));
            var request = new ReservationRequest(
                database.MemberMatricule,
                database.CourtId,
                DateOnly.FromDateTime(DateTime.Now).AddDays(4),
                new TimeOnly(10, 0),
                ReservationVisibility.Private);

            await service.CreateReservationAsync(request, CancellationToken.None);

            await Assert.ThrowsAsync<ReservationConflictException>(() =>
                service.CreateReservationAsync(request, CancellationToken.None));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [Fact]
    public async Task Reservation_overlapping_closure_is_rejected()
    {
        var database = await IntegrationDatabase.CreateAsync();

        try
        {
            var date = DateOnly.FromDateTime(DateTime.Now).AddDays(4);
            var start = date.ToDateTime(new TimeOnly(10, 0));
            await database.CreateClosureAsync(start, start.AddMinutes(90));

            var repository = new SqlAvailabilityRepository(database.Configuration);
            var service = new AvailabilityService(repository);

            await Assert.ThrowsAsync<ReservationConflictException>(() =>
                service.CreateReservationAsync(
                    new ReservationRequest(
                        database.MemberMatricule,
                        database.CourtId,
                        date,
                        new TimeOnly(10, 0),
                        ReservationVisibility.Private),
                    CancellationToken.None));
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [Fact]
    public async Task Organizer_payment_confirms_place_and_settles_existing_debt()
    {
        var database = await IntegrationDatabase.CreateAsync();

        try
        {
            var matchId = await database.CreateMatchAsync(
                DateTime.Now.AddDays(2),
                "Private",
                database.MemberId);
            var participantId = await database.CreateParticipantAsync(matchId, database.MemberId, true, "Pending");
            await database.CreateDebtAsync(matchId, database.MemberId, 30m);

            var result = await new PaymentService(new SqlPaymentRepository(database.Configuration))
                .PayParticipantAsync(matchId, database.MemberMatricule, CancellationToken.None);

            Assert.Equal(participantId, result.MatchParticipantId);
            Assert.Equal(45m, result.TotalAmount);
            Assert.Equal(30m, result.DebtAmount);

            var state = await database.QuerySingleAsync(
                """
                SELECT p.ParticipationStatus,
                       d.OutstandingAmount,
                       (SELECT COUNT(*) FROM pcm.PaymentAllocation AS pa WHERE pa.PaymentId = pay.PaymentId) AS AllocationCount
                FROM pcm.MatchParticipant AS p
                INNER JOIN pcm.Payment AS pay ON pay.PayerMemberId = p.MemberId
                LEFT JOIN pcm.Debt AS d ON d.MatchId = @MatchId
                WHERE p.MatchParticipantId = @ParticipantId;
                """,
                command =>
                {
                    command.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
                    command.Parameters.Add("@ParticipantId", SqlDbType.Int).Value = participantId;
                });

            Assert.Equal("Confirmed", state[0]);
            Assert.Equal(0m, state[1]);
            Assert.Equal(2, state[2]);

            var participant = await new SqlMatchRepository(database.Configuration)
                .GetPrivateParticipantsAsync(matchId, database.MemberId, CancellationToken.None);
            Assert.Single(participant);
            Assert.True(participant[0].IsPaid);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [Fact]
    public async Task Concurrent_private_participant_additions_stop_at_four_places()
    {
        var database = await IntegrationDatabase.CreateAsync();

        try
        {
            var matchId = await database.CreateMatchAsync(
                DateTime.Now.AddDays(2),
                "Private",
                database.MemberId);
            await database.CreateParticipantAsync(matchId, database.MemberId, true, "Pending");
            var members = new List<(int Id, string Matricule)>
            {
                (database.SecondMemberId, database.SecondMemberMatricule)
            };
            for (var index = 0; index < 3; index++)
            {
                members.Add(await database.CreateAdditionalMemberAsync());
            }

            var repository = new SqlMatchRepository(database.Configuration);
            var attempts = members.Select(member =>
                repository.AddPrivateParticipantAsync(
                    matchId,
                    database.MemberId,
                    member.Id,
                    CancellationToken.None));
            var outcomes = await Task.WhenAll(attempts.Select(IntegrationDatabase.CaptureAsync));

            Assert.Equal(3, outcomes.Count(success => success));
            Assert.Equal(1, outcomes.Count(success => !success));

            var participantCount = await database.QuerySingleAsync(
                "SELECT COUNT(*) FROM pcm.MatchParticipant WHERE MatchId = @MatchId AND ParticipationStatus <> 'Removed';",
                command => command.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId);
            Assert.Equal(4, participantCount[0]);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [Fact]
    public async Task Duplicate_private_participant_is_rejected_and_existing_place_is_preserved()
    {
        var database = await IntegrationDatabase.CreateAsync();

        try
        {
            var matchId = await database.CreateMatchAsync(
                DateTime.Now.AddDays(2),
                "Private",
                database.MemberId);
            await database.CreateParticipantAsync(matchId, database.MemberId, true, "Pending");
            var repository = new SqlMatchRepository(database.Configuration);

            await repository.AddPrivateParticipantAsync(
                matchId,
                database.MemberId,
                database.SecondMemberId,
                CancellationToken.None);

            await Assert.ThrowsAsync<ReservationConflictException>(() =>
                repository.AddPrivateParticipantAsync(
                    matchId,
                    database.MemberId,
                    database.SecondMemberId,
                    CancellationToken.None));

            var participantCount = await database.QuerySingleAsync(
                "SELECT COUNT(*) FROM pcm.MatchParticipant WHERE MatchId = @MatchId;",
                command => command.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId);
            Assert.Equal(2, participantCount[0]);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [Fact]
    public async Task Day_before_processing_publishes_match_creates_ban_and_is_idempotent()
    {
        var database = await IntegrationDatabase.CreateAsync();

        try
        {
            var now = new DateTime(2099, 7, 10, 12, 0, 0);
            var matchId = await database.CreateMatchAsync(now.AddDays(1), "Private", database.MemberId);
            var organizerParticipantId = await database.CreateParticipantAsync(matchId, database.MemberId, true, "Pending");
            var removedParticipantId = await database.CreateParticipantAsync(matchId, database.SecondMemberId, false, "Pending");

            var service = new DayBeforeService(new SqlDayBeforeRepository(database.Configuration));
            var first = await service.ProcessAsync(
                new DateTimeOffset(now, TimeSpan.FromHours(2)),
                CancellationToken.None);
            var second = await service.ProcessAsync(
                new DateTimeOffset(now, TimeSpan.FromHours(2)),
                CancellationToken.None);

            Assert.Contains(matchId, await database.QueryIdsAsync(
                "SELECT MatchId FROM pcm.Match WHERE StartsAt >= @Start AND StartsAt < @End;",
                command =>
                {
                    command.Parameters.Add("@Start", SqlDbType.DateTime2).Value = now.AddDays(1).Date;
                    command.Parameters.Add("@End", SqlDbType.DateTime2).Value = now.AddDays(2).Date;
                }));
            Assert.Equal(1, first.MatchesPublished);
            Assert.Equal(1, first.ParticipantsRemoved);
            Assert.Equal(1, first.BansCreated);
            Assert.Equal(1, first.DebtsCreated);
            Assert.Equal(0, second.MatchesPublished);
            Assert.Equal(0, second.ParticipantsRemoved);
            Assert.Equal(0, second.BansCreated);
            Assert.Equal(0, second.DebtsCreated);

            var state = await database.QuerySingleAsync(
                """
                SELECT m.Visibility,
                       (SELECT ParticipationStatus FROM pcm.MatchParticipant WHERE MatchParticipantId = @ParticipantId),
                       (SELECT ParticipationStatus FROM pcm.MatchParticipant WHERE MatchParticipantId = @RemovedParticipantId),
                       (SELECT COUNT(*) FROM pcm.BookingBan WHERE SourceMatchId = @MatchId),
                       d.OutstandingAmount
                FROM pcm.Match AS m
                LEFT JOIN pcm.Debt AS d ON d.MatchId = m.MatchId
                WHERE m.MatchId = @MatchId;
                """,
                command =>
                {
                    command.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
                    command.Parameters.Add("@ParticipantId", SqlDbType.Int).Value = organizerParticipantId;
                    command.Parameters.Add("@RemovedParticipantId", SqlDbType.Int).Value = removedParticipantId;
                });

            Assert.Equal("Public", state[0]);
            Assert.Equal("Pending", state[1]);
            Assert.Equal("Removed", state[2]);
            Assert.Equal(1, state[3]);
            Assert.Equal(60m, state[4]);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    [Fact]
    public async Task Global_statistics_report_revenue_occupancy_and_site_breakdown()
    {
        var database = await IntegrationDatabase.CreateAsync();

        try
        {
            var startsAt = new DateTime(2099, 7, 10, 11, 0, 0);
            var matchId = await database.CreateMatchAsync(startsAt, "Private", database.MemberId);
            await database.CreateParticipantAsync(matchId, database.MemberId, true, "Pending");
            await new PaymentService(new SqlPaymentRepository(database.Configuration))
                .PayParticipantAsync(matchId, database.MemberMatricule, CancellationToken.None);

            var connectionString = database.Configuration.GetConnectionString("PadelCourtManagement")!;
            var service = new StatisticsService(
                new SqlStatisticsRepository(database.Configuration),
                new SqlAdministrationRepository(connectionString),
                new SqlAdministrationRepository(connectionString),
                new AdministrationAuthorizer());

            var report = await service.GetAsync(
                "G0001",
                new StatisticsRequest(
                    startsAt.Date,
                    startsAt.Date.AddDays(1),
                    database.SiteId),
                CancellationToken.None);

            Assert.Equal(15m, report.Revenue);
            Assert.Equal(1, report.Matches);
            Assert.Equal(1, report.ConfirmedParticipations);
            Assert.Equal(4, report.Capacity);
            Assert.Contains(report.Breakdown, breakdown =>
                breakdown.SiteId == database.SiteId
                && breakdown.CourtId == database.CourtId
                && breakdown.Matches == 1
                && breakdown.ConfirmedParticipations == 1
                && breakdown.Revenue == 15m);
        }
        finally
        {
            await database.DisposeAsync();
        }
    }

    private sealed class IntegrationDatabase : IAsyncDisposable
    {
        private readonly string connectionString;
        private readonly List<int> matchIds = [];
        private readonly List<int> memberIds = [];
        private int siteId;
        private int courtId;

        private IntegrationDatabase(string connectionString, IConfiguration configuration)
        {
            this.connectionString = connectionString;
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        public int MemberId { get; private set; }
        public int SecondMemberId { get; private set; }
        public string SecondMemberMatricule { get; private set; } = string.Empty;
        public int CourtId => courtId;
        public int SiteId => siteId;
        public string MemberMatricule { get; private set; } = string.Empty;

        public static async Task<IntegrationDatabase> CreateAsync()
        {
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddUserSecrets<SqlServerIntegrationTests>(optional: true)
                .Build();
            var connectionString = configuration.GetConnectionString("PadelCourtManagement");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Set ConnectionStrings__PadelCourtManagement or configure the shared user secret to run SQL integration tests.");
            }

            var database = new IntegrationDatabase(connectionString, configuration);
            await database.InitializeAsync();
            return database;
        }

        public async Task<int> CreateMatchAsync(DateTime startsAt, string visibility, int organizerId)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(
                """
                INSERT INTO pcm.Match (CourtId, OrganizerMemberId, StartsAt, EndsAt, Visibility)
                VALUES (@CourtId, @OrganizerId, @StartsAt, DATEADD(MINUTE, 90, @StartsAt), @Visibility);
                SELECT CONVERT(INT, SCOPE_IDENTITY());
                """,
                connection);
            command.Parameters.Add("@CourtId", SqlDbType.Int).Value = courtId;
            command.Parameters.Add("@OrganizerId", SqlDbType.Int).Value = organizerId;
            command.Parameters.Add("@StartsAt", SqlDbType.DateTime2).Value = startsAt;
            command.Parameters.Add("@Visibility", SqlDbType.VarChar).Value = visibility;
            var matchId = Convert.ToInt32(await command.ExecuteScalarAsync());
            matchIds.Add(matchId);
            return matchId;
        }

        public async Task<int> CreateParticipantAsync(int matchId, int memberId, bool organizer, string status)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(
                """
                INSERT INTO pcm.MatchParticipant (MatchId, MemberId, IsOrganizer, ParticipationStatus)
                VALUES (@MatchId, @MemberId, @Organizer, @Status);
                SELECT CONVERT(INT, SCOPE_IDENTITY());
                """,
                connection);
            command.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
            command.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
            command.Parameters.Add("@Organizer", SqlDbType.Bit).Value = organizer;
            command.Parameters.Add("@Status", SqlDbType.VarChar).Value = status;
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        public async Task CreateDebtAsync(int matchId, int organizerId, decimal amount)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(
                """
                INSERT INTO pcm.Debt (OrganizerMemberId, MatchId, InitialAmount, OutstandingAmount)
                VALUES (@OrganizerId, @MatchId, @Amount, @Amount);
                """,
                connection);
            command.Parameters.Add("@OrganizerId", SqlDbType.Int).Value = organizerId;
            command.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
            command.Parameters.Add("@Amount", SqlDbType.Decimal).Value = amount;
            command.Parameters["@Amount"].Precision = 9;
            command.Parameters["@Amount"].Scale = 2;
            await command.ExecuteNonQueryAsync();
        }

        public async Task CreateClosureAsync(DateTime startsAt, DateTime endsAt)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(
                """
                INSERT INTO pcm.Closure (Scope, SiteId, StartsAt, EndsAt, Reason)
                VALUES ('S', @SiteId, @StartsAt, @EndsAt, 'Integration closure');
                SELECT CONVERT(INT, SCOPE_IDENTITY());
                """,
                connection);
            command.Parameters.Add("@SiteId", SqlDbType.Int).Value = siteId;
            command.Parameters.Add("@StartsAt", SqlDbType.DateTime2).Value = startsAt;
            command.Parameters.Add("@EndsAt", SqlDbType.DateTime2).Value = endsAt;
            await command.ExecuteNonQueryAsync();
        }

        public async Task<object[]> QuerySingleAsync(string sql, Action<SqlCommand> parameters)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            parameters(command);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                throw new InvalidOperationException("Integration query returned no rows.");
            }

            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            return values;
        }

        public async Task<IReadOnlyList<int>> QueryIdsAsync(string sql, Action<SqlCommand> parameters)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            parameters(command);
            await using var reader = await command.ExecuteReaderAsync();
            var ids = new List<int>();
            while (await reader.ReadAsync())
            {
                ids.Add(reader.GetInt32(0));
            }

            return ids;
        }

        public async ValueTask DisposeAsync()
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(
                """
                DELETE FROM pcm.PaymentAllocation
                WHERE PaymentId IN (SELECT PaymentId FROM pcm.Payment WHERE PayerMemberId IN (@MemberId, @SecondMemberId));
                DELETE FROM pcm.Payment WHERE PayerMemberId IN (@MemberId, @SecondMemberId);
                DELETE FROM pcm.Closure WHERE SiteId = @SiteId;
                DELETE FROM pcm.Debt WHERE OrganizerMemberId IN (@MemberId, @SecondMemberId);
                DELETE FROM pcm.BookingBan WHERE MemberId IN (@MemberId, @SecondMemberId);
                DELETE FROM pcm.MatchParticipant WHERE MatchId IN (SELECT MatchId FROM pcm.Match WHERE OrganizerMemberId IN (@MemberId, @SecondMemberId));
                DELETE FROM pcm.Match WHERE OrganizerMemberId IN (@MemberId, @SecondMemberId);
                DELETE FROM pcm.SiteAnnualSchedule WHERE SiteId = @SiteId;
                DELETE FROM pcm.Court WHERE CourtId = @CourtId;
                DELETE FROM pcm.Member WHERE MemberId IN (@MemberId, @SecondMemberId);
                DELETE FROM pcm.Site WHERE SiteId = @SiteId;
                """,
                connection);
            command.Parameters.Add("@MemberId", SqlDbType.Int).Value = MemberId;
            command.Parameters.Add("@SecondMemberId", SqlDbType.Int).Value = SecondMemberId;
            command.Parameters.Add("@SiteId", SqlDbType.Int).Value = siteId;
            command.Parameters.Add("@CourtId", SqlDbType.Int).Value = courtId;
            await command.ExecuteNonQueryAsync();
        }

        private async Task InitializeAsync()
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var numericSuffix = Math.Abs(Guid.NewGuid().GetHashCode()) % 10000;
            MemberMatricule = $"G{numericSuffix:0000}";
            var secondMatricule = $"L{Math.Abs(Guid.NewGuid().GetHashCode()) % 100000:00000}";
            SecondMemberMatricule = secondMatricule;
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(
                """
                INSERT INTO pcm.Site (Name) OUTPUT INSERTED.SiteId VALUES (@SiteName);
                INSERT INTO pcm.Court (SiteId, Name) OUTPUT INSERTED.CourtId VALUES (SCOPE_IDENTITY(), @CourtName);
                """,
                connection);
            command.Parameters.Add("@SiteName", SqlDbType.NVarChar, 100).Value = $"Integration {suffix}";
            command.Parameters.Add("@CourtName", SqlDbType.NVarChar, 100).Value = $"Court {suffix}";
            await using (var reader = await command.ExecuteReaderAsync())
            {
                await reader.ReadAsync();
                siteId = reader.GetInt32(0);
                await reader.NextResultAsync();
                await reader.ReadAsync();
                courtId = reader.GetInt32(0);
            }

            await using var insert = new SqlCommand(
                """
                INSERT INTO pcm.SiteAnnualSchedule (SiteId, CalendarYear, OpeningTime, ClosingTime)
                VALUES (@SiteId, YEAR(SYSUTCDATETIME()), '06:00', '23:00'),
                       (@SiteId, 2099, '06:00', '23:00');
                INSERT INTO pcm.Member (Matricule, DisplayName, MembershipCategory)
                OUTPUT INSERTED.MemberId
                VALUES (@Matricule, 'Integration organizer', 'G');
                """,
                connection);
            insert.Parameters.Add("@SiteId", SqlDbType.Int).Value = siteId;
            insert.Parameters.Add("@Matricule", SqlDbType.VarChar, 6).Value = MemberMatricule;
            MemberId = Convert.ToInt32(await insert.ExecuteScalarAsync());

            await using var second = new SqlCommand(
                """
                INSERT INTO pcm.Member (Matricule, DisplayName, MembershipCategory)
                OUTPUT INSERTED.MemberId
                VALUES (@Matricule, 'Integration participant', 'L');
                """,
                connection);
            second.Parameters.Add("@Matricule", SqlDbType.VarChar, 6).Value = secondMatricule;
            SecondMemberId = Convert.ToInt32(await second.ExecuteScalarAsync());
            memberIds.Add(MemberId);
            memberIds.Add(SecondMemberId);
        }

        public async Task<(int Id, string Matricule)> CreateAdditionalMemberAsync()
        {
            var matricule = $"L{Math.Abs(Guid.NewGuid().GetHashCode()) % 100000:00000}";
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(
                """
                INSERT INTO pcm.Member (Matricule, DisplayName, MembershipCategory)
                VALUES (@Matricule, 'Integration participant', 'L');
                SELECT CONVERT(INT, SCOPE_IDENTITY());
                """,
                connection);
            command.Parameters.Add("@Matricule", SqlDbType.VarChar, 6).Value = matricule;
            var id = Convert.ToInt32(await command.ExecuteScalarAsync());
            memberIds.Add(id);
            return (id, matricule);
        }

        public static async Task<bool> CaptureAsync(Task task)
        {
            try
            {
                await task;
                return true;
            }
            catch (ReservationConflictException)
            {
                return false;
            }
        }

    }
}
