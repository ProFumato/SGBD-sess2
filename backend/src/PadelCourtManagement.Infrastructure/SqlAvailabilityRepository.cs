using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PadelCourtManagement.Application;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Infrastructure;

public sealed class SqlAvailabilityRepository : IAvailabilityRepository
{
    private readonly string connectionString;

    public SqlAvailabilityRepository(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("PadelCourtManagement")
            ?? throw new InvalidOperationException("Missing connection string 'PadelCourtManagement'.");
    }

    public async Task<ReservationMember?> GetMemberAsync(string matricule, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT MemberId, MembershipCategory, HomeSiteId, IsActive
            FROM pcm.Member
            WHERE Matricule = @Matricule;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        Add(command, "@Matricule", SqlDbType.VarChar, matricule);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadMember(reader) : null;
    }

    public async Task<IReadOnlyList<AvailableSlot>> GetAvailabilityAsync(
        int siteId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH Schedule AS
            (
                SELECT OpeningTime, ClosingTime
                FROM pcm.SiteAnnualSchedule
                WHERE SiteId = @SiteId AND CalendarYear = @CalendarYear
            ),
            Slots AS
            (
                SELECT DATEADD(SECOND, DATEPART(SECOND, OpeningTime),
                    DATEADD(MINUTE, DATEPART(HOUR, OpeningTime) * 60 + DATEPART(MINUTE, OpeningTime), @Date)) AS StartsAt
                FROM Schedule
                WHERE DATEADD(MINUTE, 90, DATEADD(SECOND, DATEPART(SECOND, OpeningTime),
                    DATEADD(MINUTE, DATEPART(HOUR, OpeningTime) * 60 + DATEPART(MINUTE, OpeningTime), @Date)))
                    <= DATEADD(SECOND, DATEPART(SECOND, ClosingTime),
                    DATEADD(MINUTE, DATEPART(HOUR, ClosingTime) * 60 + DATEPART(MINUTE, ClosingTime), @Date))

                UNION ALL

                SELECT DATEADD(MINUTE, 105, Slots.StartsAt)
                FROM Slots
                CROSS JOIN Schedule
                WHERE DATEADD(MINUTE, 195, Slots.StartsAt) <= DATEADD(SECOND, DATEPART(SECOND, Schedule.ClosingTime),
                    DATEADD(MINUTE, DATEPART(HOUR, Schedule.ClosingTime) * 60 + DATEPART(MINUTE, Schedule.ClosingTime), @Date))
            )
            SELECT c.CourtId, c.Name, Slots.StartsAt, DATEADD(MINUTE, 90, Slots.StartsAt)
            FROM pcm.Court AS c
            CROSS JOIN Slots
            WHERE c.SiteId = @SiteId
              AND c.IsActive = 1
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM pcm.Match AS m
                  WHERE m.CourtId = c.CourtId
                    AND Slots.StartsAt < DATEADD(MINUTE, 15, m.EndsAt)
                    AND m.StartsAt < DATEADD(MINUTE, 105, Slots.StartsAt)
              )
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM pcm.Closure AS cl
                  WHERE (cl.Scope = 'G' OR cl.SiteId = @SiteId)
                    AND Slots.StartsAt < cl.EndsAt
                    AND cl.StartsAt < DATEADD(MINUTE, 90, Slots.StartsAt)
              )
            ORDER BY Slots.StartsAt, c.Name
            OPTION (MAXRECURSION 0);
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        Add(command, "@SiteId", SqlDbType.Int, siteId);
        Add(command, "@CalendarYear", SqlDbType.Int, date.Year);
        Add(command, "@Date", SqlDbType.DateTime2, date.ToDateTime(TimeOnly.MinValue));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var slots = new List<AvailableSlot>();
        while (await reader.ReadAsync(cancellationToken))
        {
            slots.Add(new AvailableSlot(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetDateTime(2),
                reader.GetDateTime(3)));
        }

        return slots;
    }

    public async Task<ReservationContext?> GetReservationContextAsync(
        string matricule,
        int courtId,
        DateTime startAt,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT m.MemberId, m.MembershipCategory, m.HomeSiteId, m.IsActive,
                   c.CourtId, c.SiteId, c.IsActive,
                   schedule.OpeningTime, schedule.ClosingTime,
                   CAST(CASE WHEN EXISTS
                   (
                       SELECT 1
                       FROM pcm.Closure AS cl
                       WHERE (cl.Scope = 'G' OR cl.SiteId = c.SiteId)
                         AND @StartsAt < cl.EndsAt
                         AND cl.StartsAt < DATEADD(MINUTE, 90, @StartsAt)
                   ) THEN 1 ELSE 0 END AS bit),
                   CAST(CASE WHEN EXISTS
                   (
                       SELECT 1 FROM pcm.Debt
                       WHERE OrganizerMemberId = m.MemberId AND OutstandingAmount > 0
                   ) THEN 1 ELSE 0 END AS bit),
                   CAST(CASE WHEN EXISTS
                   (
                       SELECT 1 FROM pcm.BookingBan
                       WHERE MemberId = m.MemberId AND StartsAt <= @Now AND EndsAt > @Now
                   ) THEN 1 ELSE 0 END AS bit)
            FROM pcm.Member AS m
            CROSS JOIN pcm.Court AS c
            OUTER APPLY
            (
                SELECT OpeningTime, ClosingTime
                FROM pcm.SiteAnnualSchedule
                WHERE SiteId = c.SiteId AND CalendarYear = DATEPART(YEAR, @StartsAt)
            ) AS schedule
            WHERE m.Matricule = @Matricule AND c.CourtId = @CourtId;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        AddContextParameters(command, matricule, courtId, startAt, now);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadContext(reader) : null;
    }

    public async Task<ReservationResult> CreateReservationAsync(
        ReservationCommand command,
        CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            await EnsureReservationStateAsync(connection, transaction, command, cancellationToken);
            await EnsureCourtAvailabilityAsync(connection, transaction, command, cancellationToken);

            var matchId = await InsertMatchAsync(connection, transaction, command, cancellationToken);
            await InsertOrganizerAsync(connection, transaction, matchId, command.MemberId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ReservationResult(
                matchId,
                command.CourtId,
                command.StartAt,
                command.StartAt.AddMinutes(90),
                command.Visibility);
        }
        catch (SqlException exception) when (exception.Number is 51000 or 51001 or 2601 or 2627)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new ReservationConflictException("The requested court slot is no longer available.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task EnsureReservationStateAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ReservationCommand command,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM pcm.Member AS m WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN pcm.Court AS c WITH (UPDLOCK, HOLDLOCK) ON c.CourtId = @CourtId
                INNER JOIN pcm.SiteAnnualSchedule AS schedule WITH (UPDLOCK, HOLDLOCK)
                    ON schedule.SiteId = c.SiteId AND schedule.CalendarYear = DATEPART(YEAR, @StartsAt)
                WHERE m.MemberId = @MemberId
                  AND m.IsActive = 1
                  AND c.IsActive = 1
                  AND (m.MembershipCategory <> 'S' OR m.HomeSiteId = c.SiteId)
                  AND
                  (
                      (m.MembershipCategory = 'G' AND CAST(@StartsAt AS date) <= DATEADD(DAY, 21, CAST(@Now AS date)))
                      OR (m.MembershipCategory = 'S' AND CAST(@StartsAt AS date) <= DATEADD(DAY, 14, CAST(@Now AS date)))
                      OR (m.MembershipCategory = 'L' AND CAST(@StartsAt AS date) <= DATEADD(DAY, 5, CAST(@Now AS date)))
                  )
                  AND @StartsAt >= DATEADD(MINUTE, DATEPART(HOUR, schedule.OpeningTime) * 60 + DATEPART(MINUTE, schedule.OpeningTime), CAST(CAST(@StartsAt AS date) AS datetime2))
                  AND DATEADD(MINUTE, 90, @StartsAt) <= DATEADD(MINUTE, DATEPART(HOUR, schedule.ClosingTime) * 60 + DATEPART(MINUTE, schedule.ClosingTime), CAST(CAST(@StartsAt AS date) AS datetime2))
                  AND NOT EXISTS (SELECT 1 FROM pcm.Debt WITH (UPDLOCK, HOLDLOCK) WHERE OrganizerMemberId = m.MemberId AND OutstandingAmount > 0)
                  AND NOT EXISTS (SELECT 1 FROM pcm.BookingBan WITH (UPDLOCK, HOLDLOCK) WHERE MemberId = m.MemberId AND StartsAt <= @Now AND EndsAt > @Now)
                  AND NOT EXISTS
                  (
                      SELECT 1 FROM pcm.Closure WITH (UPDLOCK, HOLDLOCK)
                      WHERE (Scope = 'G' OR SiteId = c.SiteId)
                        AND @StartsAt < EndsAt
                        AND StartsAt < DATEADD(MINUTE, 90, @StartsAt)
                  )
            ) THEN 1 ELSE 0 END;
            """;

        await using var commandSql = CreateCommand(connection, sql, transaction);
        Add(commandSql, "@MemberId", SqlDbType.Int, command.MemberId);
        Add(commandSql, "@CourtId", SqlDbType.Int, command.CourtId);
        Add(commandSql, "@StartsAt", SqlDbType.DateTime2, command.StartAt);
        Add(commandSql, "@Now", SqlDbType.DateTime2, command.Now);
        if (!Convert.ToBoolean(await commandSql.ExecuteScalarAsync(cancellationToken)))
        {
            throw new ReservationConflictException("Reservation eligibility changed before the match could be created.");
        }
    }

    private static async Task EnsureCourtAvailabilityAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ReservationCommand command,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1 FROM pcm.Match WITH (UPDLOCK, HOLDLOCK)
                WHERE CourtId = @CourtId
                  AND @StartsAt < DATEADD(MINUTE, 15, EndsAt)
                  AND StartsAt < DATEADD(MINUTE, 105, @StartsAt)
            ) THEN 1 ELSE 0 END;
            """;

        await using var commandSql = CreateCommand(connection, sql, transaction);
        Add(commandSql, "@CourtId", SqlDbType.Int, command.CourtId);
        Add(commandSql, "@StartsAt", SqlDbType.DateTime2, command.StartAt);
        if (Convert.ToBoolean(await commandSql.ExecuteScalarAsync(cancellationToken)))
        {
            throw new ReservationConflictException("The requested court slot is no longer available.");
        }
    }

    private static async Task<int> InsertMatchAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ReservationCommand command,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @InsertedMatch TABLE (MatchId INT);
            INSERT INTO pcm.Match (CourtId, OrganizerMemberId, StartsAt, EndsAt, Visibility)
            OUTPUT INSERTED.MatchId INTO @InsertedMatch
            VALUES (@CourtId, @MemberId, @StartsAt, DATEADD(MINUTE, 90, @StartsAt), @Visibility);
            SELECT MatchId FROM @InsertedMatch;
            """;

        await using var commandSql = CreateCommand(connection, sql, transaction);
        Add(commandSql, "@CourtId", SqlDbType.Int, command.CourtId);
        Add(commandSql, "@MemberId", SqlDbType.Int, command.MemberId);
        Add(commandSql, "@StartsAt", SqlDbType.DateTime2, command.StartAt);
        Add(commandSql, "@Visibility", SqlDbType.VarChar, command.Visibility.ToString());
        return Convert.ToInt32(await commandSql.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task InsertOrganizerAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int matchId,
        int memberId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pcm.MatchParticipant (MatchId, MemberId, IsOrganizer, ParticipationStatus)
            VALUES (@MatchId, @MemberId, 1, 'Pending');
            """;

        await using var command = CreateCommand(connection, sql, transaction);
        Add(command, "@MatchId", SqlDbType.Int, matchId);
        Add(command, "@MemberId", SqlDbType.Int, memberId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private SqlConnection CreateConnection() => new(connectionString);

    private static SqlCommand CreateCommand(SqlConnection connection, string sql, SqlTransaction? transaction = null) =>
        new(sql, connection, transaction);

    private static void Add(SqlCommand command, string name, SqlDbType type, object? value) =>
        command.Parameters.Add(name, type).Value = value ?? DBNull.Value;

    private static void AddContextParameters(
        SqlCommand command,
        string matricule,
        int courtId,
        DateTime startAt,
        DateTime now)
    {
        Add(command, "@Matricule", SqlDbType.VarChar, matricule);
        Add(command, "@CourtId", SqlDbType.Int, courtId);
        Add(command, "@StartsAt", SqlDbType.DateTime2, startAt);
        Add(command, "@Now", SqlDbType.DateTime2, now);
    }

    private static ReservationMember ReadMember(SqlDataReader reader) =>
        new(
            reader.GetInt32(0),
            reader.GetString(1) switch
            {
                "G" => MembershipCategory.Global,
                "S" => MembershipCategory.Site,
                "L" => MembershipCategory.Free,
                _ => throw new InvalidOperationException("The database contains an unknown member category.")
            },
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.GetBoolean(3));

    private static ReservationContext ReadContext(SqlDataReader reader) =>
        new(
            new ReservationMember(
                reader.GetInt32(0),
                reader.GetString(1) switch
                {
                    "G" => MembershipCategory.Global,
                    "S" => MembershipCategory.Site,
                    "L" => MembershipCategory.Free,
                    _ => throw new InvalidOperationException("The database contains an unknown member category.")
                },
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.GetBoolean(3)),
            reader.GetInt32(4),
            reader.GetInt32(5),
            reader.GetBoolean(6),
            reader.IsDBNull(7) ? null : TimeOnly.FromTimeSpan(reader.GetTimeSpan(7)),
            reader.IsDBNull(8) ? null : TimeOnly.FromTimeSpan(reader.GetTimeSpan(8)),
            reader.GetBoolean(9),
            reader.GetBoolean(10),
            reader.GetBoolean(11));
}
