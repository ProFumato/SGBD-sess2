using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PadelCourtManagement.Application;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Infrastructure;

public sealed class SqlDayBeforeRepository(IConfiguration configuration) : IDayBeforeRepository
{
    private readonly string connectionString = configuration.GetConnectionString("PadelCourtManagement")
        ?? throw new InvalidOperationException("Missing connection string 'PadelCourtManagement'.");

    public async Task<IReadOnlyList<int>> GetTomorrowMatchIdsAsync(
        DateOnly tomorrow,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT MatchId
            FROM pcm.Match
            WHERE StartsAt >= @Tomorrow
              AND StartsAt < DATEADD(DAY, 1, @Tomorrow)
            ORDER BY StartsAt, MatchId;
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Tomorrow", SqlDbType.Date).Value = tomorrow.ToDateTime(TimeOnly.MinValue);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var matchIds = new List<int>();
        while (await reader.ReadAsync(cancellationToken))
        {
            matchIds.Add(reader.GetInt32(0));
        }

        return matchIds;
    }

    public async Task<DayBeforeMatchResult> ProcessMatchAsync(
        int matchId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

        try
        {
            const string sql = """
                DECLARE @Visibility VARCHAR(7);
                DECLARE @OrganizerMemberId INT;
                DECLARE @StartsAt DATETIME2(0);
                DECLARE @RemovedCount INT = 0;
                DECLARE @Published BIT = 0;
                DECLARE @BanCreated BIT = 0;
                DECLARE @DebtCreated BIT = 0;
                DECLARE @ConfirmedCount INT;

                SELECT
                    @Visibility = Visibility,
                    @OrganizerMemberId = OrganizerMemberId,
                    @StartsAt = StartsAt
                FROM pcm.Match WITH (UPDLOCK, HOLDLOCK)
                WHERE MatchId = @MatchId;

                IF @Visibility IS NULL
                    THROW 51020, 'The match does not exist.', 1;

                IF @Visibility = 'Private'
                BEGIN
                    UPDATE pcm.MatchParticipant
                    SET ParticipationStatus = 'Removed'
                    WHERE MatchId = @MatchId
                      AND IsOrganizer = 0
                      AND ParticipationStatus = 'Pending';
                    SET @RemovedCount = @@ROWCOUNT;
                END;

                SELECT @ConfirmedCount = COUNT(*)
                FROM pcm.MatchParticipant WITH (UPDLOCK, HOLDLOCK)
                WHERE MatchId = @MatchId
                  AND ParticipationStatus = 'Confirmed';

                IF @Visibility = 'Private' AND @ConfirmedCount < 4
                BEGIN
                    UPDATE pcm.Match
                    SET Visibility = 'Public'
                    WHERE MatchId = @MatchId;
                    SET @Published = 1;

                    IF NOT EXISTS
                    (
                        SELECT 1
                        FROM pcm.BookingBan WITH (UPDLOCK, HOLDLOCK)
                        WHERE SourceMatchId = @MatchId
                    )
                    BEGIN
                        INSERT INTO pcm.BookingBan
                        (
                            MemberId,
                            SourceMatchId,
                            StartsAt,
                            EndsAt,
                            Reason
                        )
                        VALUES
                        (
                            @OrganizerMemberId,
                            @MatchId,
                            @Now,
                            DATEADD(DAY, 7, @Now),
                            N'Private match became public because it was incomplete.'
                        );
                        SET @BanCreated = 1;
                    END;
                END;

                IF (@Visibility = 'Public' OR @Published = 1) AND @ConfirmedCount < 4
                BEGIN
                    IF NOT EXISTS
                    (
                        SELECT 1
                        FROM pcm.Debt WITH (UPDLOCK, HOLDLOCK)
                        WHERE MatchId = @MatchId
                    )
                    BEGIN
                        INSERT INTO pcm.Debt
                        (
                            OrganizerMemberId,
                            MatchId,
                            InitialAmount,
                            OutstandingAmount
                        )
                        VALUES
                        (
                            @OrganizerMemberId,
                            @MatchId,
                            (4 - @ConfirmedCount) * 15.00,
                            (4 - @ConfirmedCount) * 15.00
                        );
                        SET @DebtCreated = 1;
                    END;
                END;

                SELECT @Published, @RemovedCount, @BanCreated, @DebtCreated;
                """;

            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
            command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                throw new InvalidOperationException("Day-before processing did not return a result.");
            }

            var result = new DayBeforeMatchResult(
                reader.GetBoolean(0),
                reader.GetInt32(1),
                reader.GetBoolean(2),
                reader.GetBoolean(3));
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (SqlException exception) when (exception.Number == 51020)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new ReservationNotFoundException("The match does not exist.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
