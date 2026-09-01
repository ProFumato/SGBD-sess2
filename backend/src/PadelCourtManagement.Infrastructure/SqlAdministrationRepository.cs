// SQL Administration Repository: Database access for admin data.
// Implements all admin-related repository interfaces: members, admins, sites, courts, schedules, closures.

using System.Data;
using Microsoft.Data.SqlClient;
using PadelCourtManagement.Application.Administration;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Infrastructure;

public sealed class SqlAdministrationRepository(
    string connectionString) :
    IMemberRepository,
    IAdministratorRepository,
    ISiteRepository,
    ICourtRepository,
    IScheduleRepository,
    IClosureRepository
{
    public async Task<AdministratorActor?> GetActiveAdministratorAsync(string matricule, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT m.MemberId, m.Matricule, aa.Scope, aa.SiteId
            FROM pcm.Member AS m
            INNER JOIN pcm.AdministratorAssignment AS aa ON aa.MemberId = m.MemberId
            WHERE m.Matricule = @Matricule AND m.IsActive = 1;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        Add(command, "@Matricule", SqlDbType.VarChar, matricule);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadActor(reader) : null;
    }

    public async Task<int> GetActiveGlobalAdministratorCountAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM pcm.Member AS m
            INNER JOIN pcm.AdministratorAssignment AS aa ON aa.MemberId = m.MemberId
            WHERE m.IsActive = 1 AND aa.Scope = 'G';
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<Member?> GetMemberByMatriculeAsync(string matricule, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT MemberId, Matricule, DisplayName, MembershipCategory, HomeSiteId, IsActive
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

    public async Task<IReadOnlyList<Member>> GetMembersAsync(int? siteId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT MemberId, Matricule, DisplayName, MembershipCategory, HomeSiteId, IsActive
            FROM pcm.Member
            WHERE @SiteId IS NULL
               OR (MembershipCategory = 'S' AND HomeSiteId = @SiteId)
            ORDER BY Matricule;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        Add(command, "@SiteId", SqlDbType.Int, siteId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var members = new List<Member>();
        while (await reader.ReadAsync(cancellationToken))
        {
            members.Add(ReadMember(reader));
        }

        return members;
    }

    public async Task<Member> CreateMemberAsync(MemberInput input, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pcm.Member (Matricule, DisplayName, MembershipCategory, HomeSiteId, IsActive)
            VALUES (@Matricule, @DisplayName, @MembershipCategory, @HomeSiteId, @IsActive);
            SELECT CONVERT(int, SCOPE_IDENTITY());
            """;

        var memberId = await ExecuteIdentityAsync(sql, command =>
        {
            AddMemberParameters(command, input);
        }, cancellationToken);
        return await GetMemberByIdAsync(memberId, cancellationToken);
    }

    public async Task<Member> UpdateMemberAsync(int memberId, MemberInput input, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pcm.Member
            SET Matricule = @Matricule,
                DisplayName = @DisplayName,
                MembershipCategory = @MembershipCategory,
                HomeSiteId = @HomeSiteId,
                IsActive = @IsActive
            WHERE MemberId = @MemberId;
            """;

        await ExecuteNonQueryAsync(sql, command =>
        {
            Add(command, "@MemberId", SqlDbType.Int, memberId);
            AddMemberParameters(command, input);
        }, cancellationToken);

        return await GetMemberByIdAsync(memberId, cancellationToken);
    }

    public async Task<Site?> GetSiteAsync(int siteId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT SiteId, Name FROM pcm.Site WHERE SiteId = @SiteId;";
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        Add(command, "@SiteId", SqlDbType.Int, siteId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadSite(reader) : null;
    }

    public async Task<IReadOnlyList<Site>> GetSitesAsync(int? siteId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SiteId, Name
            FROM pcm.Site
            WHERE @SiteId IS NULL OR SiteId = @SiteId
            ORDER BY Name;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        Add(command, "@SiteId", SqlDbType.Int, siteId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var sites = new List<Site>();
        while (await reader.ReadAsync(cancellationToken))
        {
            sites.Add(ReadSite(reader));
        }

        return sites;
    }

    public async Task<Site> CreateSiteAsync(SiteInput input, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pcm.Site (Name) VALUES (@Name);
            SELECT CONVERT(int, SCOPE_IDENTITY());
            """;

        var siteId = await ExecuteIdentityAsync(
            sql,
            command => Add(command, "@Name", SqlDbType.NVarChar, input.Name),
            cancellationToken);
        return await GetSiteByIdAsync(siteId, cancellationToken);
    }

    public async Task<Site> UpdateSiteAsync(int siteId, SiteInput input, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE pcm.Site SET Name = @Name WHERE SiteId = @SiteId;";
        await ExecuteNonQueryAsync(sql, command =>
        {
            Add(command, "@SiteId", SqlDbType.Int, siteId);
            Add(command, "@Name", SqlDbType.NVarChar, input.Name);
        }, cancellationToken);
        return await GetSiteByIdAsync(siteId, cancellationToken);
    }

    public async Task<Court?> GetCourtAsync(int courtId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT CourtId, SiteId, Name, IsActive FROM pcm.Court WHERE CourtId = @CourtId;";
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        Add(command, "@CourtId", SqlDbType.Int, courtId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadCourt(reader) : null;
    }

    public async Task<IReadOnlyList<Court>> GetCourtsAsync(int siteId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CourtId, SiteId, Name, IsActive
            FROM pcm.Court
            WHERE SiteId = @SiteId
            ORDER BY Name;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        Add(command, "@SiteId", SqlDbType.Int, siteId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var courts = new List<Court>();
        while (await reader.ReadAsync(cancellationToken))
        {
            courts.Add(ReadCourt(reader));
        }

        return courts;
    }

    public async Task<Court> CreateCourtAsync(int siteId, CourtInput input, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pcm.Court (SiteId, Name, IsActive)
            VALUES (@SiteId, @Name, @IsActive);
            SELECT CONVERT(int, SCOPE_IDENTITY());
            """;

        var courtId = await ExecuteIdentityAsync(sql, command =>
        {
            Add(command, "@SiteId", SqlDbType.Int, siteId);
            Add(command, "@Name", SqlDbType.NVarChar, input.Name);
            Add(command, "@IsActive", SqlDbType.Bit, input.IsActive);
        }, cancellationToken);
        return await GetCourtByIdAsync(courtId, cancellationToken);
    }

    public async Task<Court> UpdateCourtAsync(int courtId, CourtInput input, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pcm.Court
            SET Name = @Name, IsActive = @IsActive
            WHERE CourtId = @CourtId;
            """;

        await ExecuteNonQueryAsync(sql, command =>
        {
            Add(command, "@CourtId", SqlDbType.Int, courtId);
            Add(command, "@Name", SqlDbType.NVarChar, input.Name);
            Add(command, "@IsActive", SqlDbType.Bit, input.IsActive);
        }, cancellationToken);
        return await GetCourtByIdAsync(courtId, cancellationToken);
    }

    public async Task<bool> HasMatchOutsideScheduleAsync(
        int siteId,
        int calendarYear,
        TimeOnly openingTime,
        TimeOnly closingTime,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM pcm.Match AS m
                INNER JOIN pcm.Court AS c ON c.CourtId = m.CourtId
                WHERE c.SiteId = @SiteId
                  AND DATEPART(YEAR, m.StartsAt) = @CalendarYear
                  AND
                  (
                      CONVERT(time(0), m.StartsAt) < @OpeningTime
                      OR CONVERT(time(0), m.EndsAt) > @ClosingTime
                  )
            ) THEN 1 ELSE 0 END;
            """;

        return await ExecuteBooleanAsync(sql, command =>
        {
            Add(command, "@SiteId", SqlDbType.Int, siteId);
            Add(command, "@CalendarYear", SqlDbType.Int, calendarYear);
            Add(command, "@OpeningTime", SqlDbType.Time, openingTime.ToTimeSpan());
            Add(command, "@ClosingTime", SqlDbType.Time, closingTime.ToTimeSpan());
        }, cancellationToken);
    }

    public async Task<bool> HasMatchesInYearAsync(int siteId, int calendarYear, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM pcm.Match AS m
                INNER JOIN pcm.Court AS c ON c.CourtId = m.CourtId
                WHERE c.SiteId = @SiteId
                  AND DATEPART(YEAR, m.StartsAt) = @CalendarYear
            ) THEN 1 ELSE 0 END;
            """;

        return await ExecuteBooleanAsync(sql, command =>
        {
            Add(command, "@SiteId", SqlDbType.Int, siteId);
            Add(command, "@CalendarYear", SqlDbType.Int, calendarYear);
        }, cancellationToken);
    }

    public async Task<SiteAnnualSchedule> SetScheduleAsync(
        int siteId,
        int calendarYear,
        ScheduleInput input,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pcm.SiteAnnualSchedule
            SET OpeningTime = @OpeningTime, ClosingTime = @ClosingTime
            WHERE SiteId = @SiteId AND CalendarYear = @CalendarYear;

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO pcm.SiteAnnualSchedule (SiteId, CalendarYear, OpeningTime, ClosingTime)
                VALUES (@SiteId, @CalendarYear, @OpeningTime, @ClosingTime);
            END;

            SELECT SiteAnnualScheduleId, SiteId, CalendarYear, OpeningTime, ClosingTime
            FROM pcm.SiteAnnualSchedule
            WHERE SiteId = @SiteId AND CalendarYear = @CalendarYear;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        AddScheduleParameters(command, siteId, calendarYear, input.GetOpeningTime(), input.GetClosingTime());
        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (!reader.HasRows && await reader.NextResultAsync(cancellationToken))
            {
            }

            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("The schedule could not be read after it was saved.");
            }

            return ReadSchedule(reader);
        }
        catch (SqlException exception) when (IsIntegrityConflict(exception))
        {
            throw new AdministrationConflictException(GetIntegrityConflictMessage(exception));
        }
    }

    public async Task<IReadOnlyList<SiteAnnualSchedule>> GetSchedulesAsync(int siteId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SiteAnnualScheduleId, SiteId, CalendarYear, OpeningTime, ClosingTime
            FROM pcm.SiteAnnualSchedule
            WHERE SiteId = @SiteId
            ORDER BY CalendarYear;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        Add(command, "@SiteId", SqlDbType.Int, siteId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var schedules = new List<SiteAnnualSchedule>();
        while (await reader.ReadAsync(cancellationToken))
        {
            schedules.Add(ReadSchedule(reader));
        }

        return schedules;
    }

    public Task DeleteScheduleAsync(int siteId, int calendarYear, CancellationToken cancellationToken) =>
        ExecuteNonQueryAsync(
            "DELETE FROM pcm.SiteAnnualSchedule WHERE SiteId = @SiteId AND CalendarYear = @CalendarYear;",
            command =>
            {
                Add(command, "@SiteId", SqlDbType.Int, siteId);
                Add(command, "@CalendarYear", SqlDbType.Int, calendarYear);
            },
            cancellationToken);

    public async Task<Closure?> GetClosureAsync(int closureId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ClosureId, Scope, SiteId, StartsAt, EndsAt, Reason
            FROM pcm.Closure
            WHERE ClosureId = @ClosureId;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        Add(command, "@ClosureId", SqlDbType.Int, closureId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadClosure(reader) : null;
    }

    public async Task<IReadOnlyList<Closure>> GetClosuresAsync(int? siteId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ClosureId, Scope, SiteId, StartsAt, EndsAt, Reason
            FROM pcm.Closure
            WHERE @SiteId IS NULL OR Scope = 'G' OR (Scope = 'S' AND SiteId = @SiteId)
            ORDER BY StartsAt;
            """;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        Add(command, "@SiteId", SqlDbType.Int, siteId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var closures = new List<Closure>();
        while (await reader.ReadAsync(cancellationToken))
        {
            closures.Add(ReadClosure(reader));
        }

        return closures;
    }

    public async Task<bool> HasMatchOverlappingClosureAsync(ClosureInput input, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM pcm.Match AS m
                INNER JOIN pcm.Court AS c ON c.CourtId = m.CourtId
                WHERE (@SiteId IS NULL OR c.SiteId = @SiteId)
                  AND m.StartsAt < @EndsAt
                  AND @StartsAt < m.EndsAt
            ) THEN 1 ELSE 0 END;
            """;

        return await ExecuteBooleanAsync(sql, command =>
        {
            Add(command, "@SiteId", SqlDbType.Int, input.Scope == ClosureScope.Site ? input.SiteId : null);
            Add(command, "@StartsAt", SqlDbType.DateTime2, input.StartsAt);
            Add(command, "@EndsAt", SqlDbType.DateTime2, input.EndsAt);
        }, cancellationToken);
    }

    public async Task<Closure> CreateClosureAsync(ClosureInput input, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO pcm.Closure (Scope, SiteId, StartsAt, EndsAt, Reason)
            VALUES (@Scope, @SiteId, @StartsAt, @EndsAt, @Reason);
            SELECT CONVERT(int, SCOPE_IDENTITY());
            """;

        var closureId = await ExecuteIdentityAsync(
            sql,
            command => AddClosureParameters(command, input),
            cancellationToken);
        return await GetClosureByIdAsync(closureId, cancellationToken);
    }

    public async Task<Closure> UpdateClosureAsync(int closureId, ClosureInput input, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE pcm.Closure
            SET Scope = @Scope,
                SiteId = @SiteId,
                StartsAt = @StartsAt,
                EndsAt = @EndsAt,
                Reason = @Reason
            WHERE ClosureId = @ClosureId;
            """;

        await ExecuteNonQueryAsync(sql, command =>
        {
            Add(command, "@ClosureId", SqlDbType.Int, closureId);
            AddClosureParameters(command, input);
        }, cancellationToken);
        return await GetClosureByIdAsync(closureId, cancellationToken);
    }

    public Task DeleteClosureAsync(int closureId, CancellationToken cancellationToken) =>
        ExecuteNonQueryAsync(
            "DELETE FROM pcm.Closure WHERE ClosureId = @ClosureId;",
            command => Add(command, "@ClosureId", SqlDbType.Int, closureId),
            cancellationToken);

    public Task SetAdministratorRoleAsync(int memberId, AdministratorRoleInput input, CancellationToken cancellationToken) =>
        ExecuteNonQueryAsync(
            """
            UPDATE pcm.AdministratorAssignment
            SET Scope = @Scope, SiteId = @SiteId
            WHERE MemberId = @MemberId;

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO pcm.AdministratorAssignment (MemberId, Scope, SiteId)
                VALUES (@MemberId, @Scope, @SiteId);
            END;
            """,
            command =>
            {
                Add(command, "@MemberId", SqlDbType.Int, memberId);
                Add(command, "@Scope", SqlDbType.Char, ScopeToDatabase(input.Scope));
                Add(command, "@SiteId", SqlDbType.Int, input.SiteId);
            },
            cancellationToken);

    public Task RemoveAdministratorRoleAsync(int memberId, CancellationToken cancellationToken) =>
        ExecuteNonQueryAsync(
            "DELETE FROM pcm.AdministratorAssignment WHERE MemberId = @MemberId;",
            command => Add(command, "@MemberId", SqlDbType.Int, memberId),
            cancellationToken);

    private async Task<Member> GetMemberByIdAsync(int memberId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT MemberId, Matricule, DisplayName, MembershipCategory, HomeSiteId, IsActive
            FROM pcm.Member
            WHERE MemberId = @MemberId;
            """;
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        Add(command, "@MemberId", SqlDbType.Int, memberId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("The member could not be read after it was saved.");
        }

        return ReadMember(reader);
    }

    private async Task<Site> GetSiteByIdAsync(int siteId, CancellationToken cancellationToken) =>
        await GetSiteAsync(siteId, cancellationToken) ?? throw new InvalidOperationException("The site could not be read after it was saved.");

    private async Task<Court> GetCourtByIdAsync(int courtId, CancellationToken cancellationToken) =>
        await GetCourtAsync(courtId, cancellationToken) ?? throw new InvalidOperationException("The court could not be read after it was saved.");

    private async Task<Closure> GetClosureByIdAsync(int closureId, CancellationToken cancellationToken) =>
        await GetClosureAsync(closureId, cancellationToken) ?? throw new InvalidOperationException("The closure could not be read after it was saved.");

    private async Task<int> ExecuteIdentityAsync(string sql, Action<SqlCommand> configure, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        configure(command);
        try
        {
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(value);
        }
        catch (SqlException exception) when (IsIntegrityConflict(exception))
        {
            throw new AdministrationConflictException(GetIntegrityConflictMessage(exception));
        }
    }

    private async Task ExecuteNonQueryAsync(
        string sql,
        Action<SqlCommand> configure,
        CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        configure(command);
        try
        {
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (SqlException exception) when (IsIntegrityConflict(exception))
        {
            throw new AdministrationConflictException(GetIntegrityConflictMessage(exception));
        }
    }

    private async Task<bool> ExecuteBooleanAsync(
        string sql,
        Action<SqlCommand> configure,
        CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = CreateCommand(connection, sql);
        configure(command);
        return Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
    }

    private SqlConnection CreateConnection()
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "The PadelCourtManagement connection string must be configured outside source control.");
        }

        return new SqlConnection(connectionString);
    }

    private static SqlCommand CreateCommand(SqlConnection connection, string sql) =>
        new(sql, connection);

    private static void Add(SqlCommand command, string name, SqlDbType type, object? value)
    {
        command.Parameters.Add(name, type).Value = value ?? DBNull.Value;
    }

    private static void AddMemberParameters(SqlCommand command, MemberInput input)
    {
        Add(command, "@Matricule", SqlDbType.VarChar, input.Matricule);
        Add(command, "@DisplayName", SqlDbType.NVarChar, input.DisplayName);
        Add(command, "@MembershipCategory", SqlDbType.Char, CategoryToDatabase(input.MembershipCategory));
        Add(command, "@HomeSiteId", SqlDbType.Int, input.HomeSiteId);
        Add(command, "@IsActive", SqlDbType.Bit, input.IsActive);
    }

    private static void AddScheduleParameters(
        SqlCommand command,
        int siteId,
        int calendarYear,
        TimeOnly openingTime,
        TimeOnly closingTime)
    {
        Add(command, "@SiteId", SqlDbType.Int, siteId);
        Add(command, "@CalendarYear", SqlDbType.SmallInt, calendarYear);
        Add(command, "@OpeningTime", SqlDbType.Time, openingTime.ToTimeSpan());
        Add(command, "@ClosingTime", SqlDbType.Time, closingTime.ToTimeSpan());
    }

    private static void AddClosureParameters(SqlCommand command, ClosureInput input)
    {
        Add(command, "@Scope", SqlDbType.Char, ClosureScopeToDatabase(input.Scope));
        Add(command, "@SiteId", SqlDbType.Int, input.SiteId);
        Add(command, "@StartsAt", SqlDbType.DateTime2, input.StartsAt);
        Add(command, "@EndsAt", SqlDbType.DateTime2, input.EndsAt);
        Add(command, "@Reason", SqlDbType.NVarChar, input.Reason);
    }

    private static Member ReadMember(SqlDataReader reader) =>
        new(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            CategoryFromDatabase(reader.GetString(3)),
            reader.IsDBNull(4) ? null : reader.GetInt32(4),
            reader.GetBoolean(5));

    private static AdministratorActor ReadActor(SqlDataReader reader) =>
        new(
            reader.GetInt32(0),
            reader.GetString(1),
            AdministratorScopeFromDatabase(reader.GetString(2)),
            reader.IsDBNull(3) ? null : reader.GetInt32(3));

    private static Site ReadSite(SqlDataReader reader) =>
        new(reader.GetInt32(0), reader.GetString(1));

    private static Court ReadCourt(SqlDataReader reader) =>
        new(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetBoolean(3));

    private static SiteAnnualSchedule ReadSchedule(SqlDataReader reader) =>
        new(
            reader.GetInt32(0),
            reader.GetInt32(1),
            Convert.ToInt32(reader.GetInt16(2)),
            TimeOnly.FromTimeSpan(reader.GetTimeSpan(3)),
            TimeOnly.FromTimeSpan(reader.GetTimeSpan(4)));

    private static Closure ReadClosure(SqlDataReader reader) =>
        new(
            reader.GetInt32(0),
            ClosureScopeFromDatabase(reader.GetString(1)),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.GetDateTime(3),
            reader.GetDateTime(4),
            reader.GetString(5));

    private static string CategoryToDatabase(MembershipCategory category) => category switch
    {
        MembershipCategory.Global => "G",
        MembershipCategory.Site => "S",
        MembershipCategory.Free => "L",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };

    private static MembershipCategory CategoryFromDatabase(string category) => category switch
    {
        "G" => MembershipCategory.Global,
        "S" => MembershipCategory.Site,
        "L" => MembershipCategory.Free,
        _ => throw new InvalidOperationException("The database contains an unknown member category.")
    };

    private static string ScopeToDatabase(AdministratorScope scope) => scope switch
    {
        AdministratorScope.Global => "G",
        AdministratorScope.Site => "S",
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    private static string ClosureScopeToDatabase(ClosureScope scope) => scope switch
    {
        ClosureScope.Global => "G",
        ClosureScope.Site => "S",
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };

    private static ClosureScope ClosureScopeFromDatabase(string scope) => scope switch
    {
        "G" => ClosureScope.Global,
        "S" => ClosureScope.Site,
        _ => throw new InvalidOperationException("The database contains an unknown closure scope.")
    };

    private static AdministratorScope AdministratorScopeFromDatabase(string scope) => scope switch
    {
        "G" => AdministratorScope.Global,
        "S" => AdministratorScope.Site,
        _ => throw new InvalidOperationException("The database contains an unknown administrator scope.")
    };

    private static bool IsIntegrityConflict(SqlException exception) =>
        exception.Number is 2601 or 2627 or 51002 or 51006 or 51007;

    private static string GetIntegrityConflictMessage(SqlException exception) => exception.Number switch
    {
        51002 => "A closure cannot overlap an existing match.",
        51006 => "At least one active global administrator must remain assigned.",
        51007 => "The schedule would make an existing match fall outside the site's opening hours.",
        _ => "The requested change conflicts with existing data."
    };
}
