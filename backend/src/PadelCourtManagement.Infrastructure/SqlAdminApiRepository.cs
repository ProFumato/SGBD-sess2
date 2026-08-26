using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PadelCourtManagement.Application;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Infrastructure;

public sealed class SqlAdminApiRepository : IAdminApiRepository
{
    private readonly string connectionString;

    public SqlAdminApiRepository(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("PadelCourtManagement")
            ?? throw new InvalidOperationException("Missing connection string 'PadelCourtManagement'.");
    }

    public IReadOnlyList<MemberRecord> GetMembers() => QueryMembers();
    public int CreateMember(MemberRequest request) => ExecuteInsert(cmd =>
    {
        cmd.CommandText = """
            INSERT INTO [pcm].[Member] ([Matricule], [DisplayName], [MembershipCategory], [HomeSiteId])
            OUTPUT INSERTED.[MemberId]
            VALUES (@Matricule, @DisplayName, @Category, @HomeSiteId);
            """;
        cmd.Parameters.Add(new SqlParameter("@Matricule", SqlDbType.VarChar, 6) { Value = request.Matricule });
        cmd.Parameters.Add(new SqlParameter("@DisplayName", SqlDbType.NVarChar, 120) { Value = request.DisplayName });
        cmd.Parameters.Add(new SqlParameter("@Category", SqlDbType.Char, 1) { Value = request.MembershipCategory });
        cmd.Parameters.Add(new SqlParameter("@HomeSiteId", SqlDbType.Int) { Value = (object?)request.HomeSiteId ?? DBNull.Value });
    });

    public int UpdateMember(int memberId, MemberRequest request) => ExecuteExecuteScalar(cmd =>
    {
        cmd.CommandText = """
            UPDATE [pcm].[Member]
            SET [Matricule] = @Matricule,
                [DisplayName] = @DisplayName,
                [MembershipCategory] = @Category,
                [HomeSiteId] = @HomeSiteId
            WHERE [MemberId] = @Id;
            SELECT @Id;
            """;
        cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = memberId });
        cmd.Parameters.Add(new SqlParameter("@Matricule", SqlDbType.VarChar, 6) { Value = request.Matricule });
        cmd.Parameters.Add(new SqlParameter("@DisplayName", SqlDbType.NVarChar, 120) { Value = request.DisplayName });
        cmd.Parameters.Add(new SqlParameter("@Category", SqlDbType.Char, 1) { Value = request.MembershipCategory });
        cmd.Parameters.Add(new SqlParameter("@HomeSiteId", SqlDbType.Int) { Value = (object?)request.HomeSiteId ?? DBNull.Value });
    });
    public void DeleteMember(int memberId) => ExecuteNonQuery("DELETE FROM [pcm].[Member] WHERE [MemberId] = @Id;", memberId);

    public IReadOnlyList<SiteRecord> GetSites() => QuerySites();
    public int CreateSite(SiteRequest request) => ExecuteInsert(cmd =>
    {
        cmd.CommandText = """
            INSERT INTO [pcm].[Site] ([Name])
            OUTPUT INSERTED.[SiteId]
            VALUES (@Name);
            """;
        cmd.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, 100) { Value = request.Name });
    });
    public int UpdateSite(int siteId, SiteRequest request) => ExecuteExecuteScalar(cmd =>
    {
        cmd.CommandText = "UPDATE [pcm].[Site] SET [Name] = @Name WHERE [SiteId] = @Id; SELECT @Id;";
        cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = siteId });
        cmd.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, 100) { Value = request.Name });
    });
    public void DeleteSite(int siteId) => ExecuteNonQuery("DELETE FROM [pcm].[Site] WHERE [SiteId] = @Id;", siteId);

    public IReadOnlyList<CourtRecord> GetCourts() => QueryCourts();
    public int CreateCourt(CourtRequest request) => ExecuteInsert(cmd =>
    {
        cmd.CommandText = """
            INSERT INTO [pcm].[Court] ([SiteId], [Name], [IsActive])
            OUTPUT INSERTED.[CourtId]
            VALUES (@SiteId, @Name, @IsActive);
            """;
        cmd.Parameters.Add(new SqlParameter("@SiteId", SqlDbType.Int) { Value = request.SiteId });
        cmd.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, 100) { Value = request.Name });
        cmd.Parameters.Add(new SqlParameter("@IsActive", SqlDbType.Bit) { Value = request.IsActive });
    });
    public int UpdateCourt(int courtId, CourtRequest request) => ExecuteExecuteScalar(cmd =>
    {
        cmd.CommandText = "UPDATE [pcm].[Court] SET [SiteId] = @SiteId, [Name] = @Name, [IsActive] = @IsActive WHERE [CourtId] = @Id; SELECT @Id;";
        cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = courtId });
        cmd.Parameters.Add(new SqlParameter("@SiteId", SqlDbType.Int) { Value = request.SiteId });
        cmd.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, 100) { Value = request.Name });
        cmd.Parameters.Add(new SqlParameter("@IsActive", SqlDbType.Bit) { Value = request.IsActive });
    });
    public void DeleteCourt(int courtId) => ExecuteNonQuery("DELETE FROM [pcm].[Court] WHERE [CourtId] = @Id;", courtId);

    public IReadOnlyList<ScheduleRecord> GetSchedules() => QuerySchedules();
    public int CreateSchedule(ScheduleRequest request) => ExecuteInsert(cmd =>
    {
        cmd.CommandText = """
            INSERT INTO [pcm].[SiteAnnualSchedule] ([SiteId], [CalendarYear], [OpeningTime], [ClosingTime])
            OUTPUT INSERTED.[SiteAnnualScheduleId]
            VALUES (@SiteId, @Year, @OpeningTime, @ClosingTime);
            """;
        cmd.Parameters.Add(new SqlParameter("@SiteId", SqlDbType.Int) { Value = request.SiteId });
        cmd.Parameters.Add(new SqlParameter("@Year", SqlDbType.SmallInt) { Value = request.CalendarYear });
        cmd.Parameters.Add(new SqlParameter("@OpeningTime", SqlDbType.Time) { Value = request.OpeningTime.ToTimeSpan() });
        cmd.Parameters.Add(new SqlParameter("@ClosingTime", SqlDbType.Time) { Value = request.ClosingTime.ToTimeSpan() });
    });
    public int UpdateSchedule(int scheduleId, ScheduleRequest request) => ExecuteExecuteScalar(cmd =>
    {
        cmd.CommandText = "UPDATE [pcm].[SiteAnnualSchedule] SET [SiteId] = @SiteId, [CalendarYear] = @Year, [OpeningTime] = @OpeningTime, [ClosingTime] = @ClosingTime WHERE [SiteAnnualScheduleId] = @Id; SELECT @Id;";
        cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = scheduleId });
        cmd.Parameters.Add(new SqlParameter("@SiteId", SqlDbType.Int) { Value = request.SiteId });
        cmd.Parameters.Add(new SqlParameter("@Year", SqlDbType.SmallInt) { Value = request.CalendarYear });
        cmd.Parameters.Add(new SqlParameter("@OpeningTime", SqlDbType.Time) { Value = request.OpeningTime.ToTimeSpan() });
        cmd.Parameters.Add(new SqlParameter("@ClosingTime", SqlDbType.Time) { Value = request.ClosingTime.ToTimeSpan() });
    });
    public void DeleteSchedule(int scheduleId) => ExecuteNonQuery("DELETE FROM [pcm].[SiteAnnualSchedule] WHERE [SiteAnnualScheduleId] = @Id;", scheduleId);

    public IReadOnlyList<ClosureRecord> GetClosures() => QueryClosures();
    public int CreateClosure(ClosureRequest request) => ExecuteInsert(cmd =>
    {
        cmd.CommandText = """
            INSERT INTO [pcm].[Closure] ([Scope], [SiteId], [StartsAt], [EndsAt], [Reason])
            OUTPUT INSERTED.[ClosureId]
            VALUES (@Scope, @SiteId, @StartsAt, @EndsAt, @Reason);
            """;
        cmd.Parameters.Add(new SqlParameter("@Scope", SqlDbType.Char, 1) { Value = request.Scope });
        cmd.Parameters.Add(new SqlParameter("@SiteId", SqlDbType.Int) { Value = (object?)request.SiteId ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@StartsAt", SqlDbType.DateTime2) { Value = request.StartsAt.DateTime });
        cmd.Parameters.Add(new SqlParameter("@EndsAt", SqlDbType.DateTime2) { Value = request.EndsAt.DateTime });
        cmd.Parameters.Add(new SqlParameter("@Reason", SqlDbType.NVarChar, 250) { Value = request.Reason });
    });
    public int UpdateClosure(int closureId, ClosureRequest request) => ExecuteExecuteScalar(cmd =>
    {
        cmd.CommandText = "UPDATE [pcm].[Closure] SET [Scope] = @Scope, [SiteId] = @SiteId, [StartsAt] = @StartsAt, [EndsAt] = @EndsAt, [Reason] = @Reason WHERE [ClosureId] = @Id; SELECT @Id;";
        cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = closureId });
        cmd.Parameters.Add(new SqlParameter("@Scope", SqlDbType.Char, 1) { Value = request.Scope });
        cmd.Parameters.Add(new SqlParameter("@SiteId", SqlDbType.Int) { Value = (object?)request.SiteId ?? DBNull.Value });
        cmd.Parameters.Add(new SqlParameter("@StartsAt", SqlDbType.DateTime2) { Value = request.StartsAt.DateTime });
        cmd.Parameters.Add(new SqlParameter("@EndsAt", SqlDbType.DateTime2) { Value = request.EndsAt.DateTime });
        cmd.Parameters.Add(new SqlParameter("@Reason", SqlDbType.NVarChar, 250) { Value = request.Reason });
    });
    public void DeleteClosure(int closureId) => ExecuteNonQuery("DELETE FROM [pcm].[Closure] WHERE [ClosureId] = @Id;", closureId);

    private IReadOnlyList<MemberRecord> QueryMembers() => QueryList("SELECT [MemberId], [Matricule], [DisplayName], [MembershipCategory], [HomeSiteId], [IsActive] FROM [pcm].[Member] ORDER BY [MemberId]", reader =>
        new MemberRecord(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)[0], reader.IsDBNull(4) ? null : reader.GetInt32(4), reader.GetBoolean(5)));
    private IReadOnlyList<SiteRecord> QuerySites() => QueryList("SELECT [SiteId], [Name] FROM [pcm].[Site] ORDER BY [SiteId]", reader => new SiteRecord(reader.GetInt32(0), reader.GetString(1)));
    private IReadOnlyList<CourtRecord> QueryCourts() => QueryList("SELECT [CourtId], [SiteId], [Name], [IsActive] FROM [pcm].[Court] ORDER BY [CourtId]", reader => new CourtRecord(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetBoolean(3)));
    private IReadOnlyList<ScheduleRecord> QuerySchedules() => QueryList("SELECT [SiteAnnualScheduleId], [SiteId], [CalendarYear], [OpeningTime], [ClosingTime] FROM [pcm].[SiteAnnualSchedule] ORDER BY [SiteAnnualScheduleId]", reader => new ScheduleRecord(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt16(2), TimeOnly.FromTimeSpan(reader.GetTimeSpan(3)), TimeOnly.FromTimeSpan(reader.GetTimeSpan(4))));
    private IReadOnlyList<ClosureRecord> QueryClosures() => QueryList("SELECT [ClosureId], [Scope], [SiteId], [StartsAt], [EndsAt], [Reason] FROM [pcm].[Closure] ORDER BY [ClosureId]", reader => new ClosureRecord(reader.GetInt32(0), reader.GetString(1)[0], reader.IsDBNull(2) ? null : reader.GetInt32(2), new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero), new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero), reader.GetString(5)));

    private int ExecuteInsert(Action<SqlCommand> configure)
        => ExecuteScalar(configure);
    private int ExecuteExecuteScalar(Action<SqlCommand> configure)
        => ExecuteScalar(configure);
    private void ExecuteNonQuery(string sql, int id)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
        command.ExecuteNonQuery();
    }
    private int ExecuteScalar(Action<SqlCommand> configure)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        configure(command);
        return (int)command.ExecuteScalar()!;
    }
    private IReadOnlyList<T> QueryList<T>(string sql, Func<SqlDataReader, T> map)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var items = new List<T>();
        while (reader.Read()) items.Add(map(reader));
        return items;
    }
}
