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

    public IReadOnlyList<AvailableSlot> GetAvailability(AvailabilityRequest request)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT TOP (1) c.[Name]
            FROM [pcm].[Court] c
            INNER JOIN [pcm].[Site] s ON s.[SiteId] = c.[SiteId]
            WHERE c.[IsActive] = 1
            ORDER BY c.[CourtId];
            """;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return Array.Empty<AvailableSlot>();
        }

        var start = request.Date.ToDateTime(request.StartTime, DateTimeKind.Unspecified);
        var slotStart = new DateTimeOffset(start, TimeSpan.FromHours(2));
        var slotEnd = slotStart.AddMinutes(90);
        return new[]
        {
            new AvailableSlot(reader.GetString(0), slotStart, slotEnd)
        };
    }

    public ReservationResult CreateReservation(ReservationRequest request)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var courtId = ResolveCourtId(connection, transaction, request.CourtCode);
        EnsureCourtIsAvailable(connection, transaction, courtId, request.Start);
        var reservationCode = InsertMatch(connection, transaction, courtId, request);
        transaction.Commit();

        return reservationCode;
    }

    private static ReservationResult InsertMatch(SqlConnection connection, SqlTransaction transaction, int courtId, ReservationRequest request)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO [pcm].[Match] ([CourtId], [OrganizerMemberId], [StartsAt], [EndsAt], [Visibility])
            OUTPUT INSERTED.[MatchId]
            VALUES (
                @CourtId,
                (SELECT [MemberId] FROM [pcm].[Member] WHERE [Matricule] = @Matricule),
                @StartsAt,
                DATEADD(MINUTE, 90, @StartsAt),
                @Visibility);
            """;
        command.Parameters.Add(new SqlParameter("@CourtId", SqlDbType.Int) { Value = courtId });
        command.Parameters.Add(new SqlParameter("@Matricule", SqlDbType.VarChar, 6) { Value = request.Matricule });
        command.Parameters.Add(new SqlParameter("@StartsAt", SqlDbType.DateTime2) { Value = request.Start.DateTime });
        command.Parameters.Add(new SqlParameter("@Visibility", SqlDbType.VarChar, 7) { Value = request.Visibility == ReservationVisibility.Public ? "Public" : "Private" });

        var matchId = (int)command.ExecuteScalar()!;
        return new ReservationResult($"M{matchId:0000}", request.CourtCode, request.Start, request.Start.AddMinutes(90));
    }

    private static int ResolveCourtId(SqlConnection connection, SqlTransaction transaction, string courtCode)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT TOP (1) [CourtId] FROM [pcm].[Court] WHERE [Name] = @CourtCode;";
        command.Parameters.Add(new SqlParameter("@CourtCode", SqlDbType.NVarChar, 100) { Value = courtCode });

        var result = command.ExecuteScalar();
        if (result is null)
        {
            throw new InvalidOperationException($"Unknown court '{courtCode}'.");
        }

        return (int)result;
    }

    private static void EnsureCourtIsAvailable(SqlConnection connection, SqlTransaction transaction, int courtId, DateTimeOffset start)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COUNT(1)
            FROM [pcm].[Match]
            WHERE [CourtId] = @CourtId
              AND @StartsAt < DATEADD(MINUTE, 15, [EndsAt])
              AND [StartsAt] < DATEADD(MINUTE, 15, DATEADD(MINUTE, 90, @StartsAt));
            """;
        command.Parameters.Add(new SqlParameter("@CourtId", SqlDbType.Int) { Value = courtId });
        command.Parameters.Add(new SqlParameter("@StartsAt", SqlDbType.DateTime2) { Value = start.DateTime });

        var conflicts = (int)command.ExecuteScalar()!;
        if (conflicts > 0)
        {
            throw new InvalidOperationException("The court is already reserved for the requested time slot.");
        }
    }
}
