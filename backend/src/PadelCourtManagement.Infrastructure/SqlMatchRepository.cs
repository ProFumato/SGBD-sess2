using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PadelCourtManagement.Application;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Infrastructure;

public sealed class SqlMatchRepository(IConfiguration configuration) : IMatchRepository
{
    private readonly string connectionString = configuration.GetConnectionString("PadelCourtManagement")
        ?? throw new InvalidOperationException("Missing connection string 'PadelCourtManagement'.");

    public async Task<ReservationMember?> GetMemberAsync(string matricule, CancellationToken cancellationToken)
    {
        const string sql = "SELECT MemberId, MembershipCategory, HomeSiteId, IsActive FROM pcm.Member WHERE Matricule = @Matricule;";
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Matricule", SqlDbType.VarChar).Value = matricule;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ReservationMember(
            reader.GetInt32(0),
            Category(reader.GetString(1)),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.GetBoolean(3));
    }

    public async Task<MatchDetails?> GetMatchAsync(int matchId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT MatchId, OrganizerMemberId, Visibility, StartsAt FROM pcm.Match WHERE MatchId = @MatchId;";
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new MatchDetails(reader.GetInt32(0), reader.GetInt32(1), Visibility(reader.GetString(2)), reader.GetDateTime(3))
            : null;
    }

    public async Task AddPrivateParticipantAsync(int matchId, int organizerMemberId, int participantMemberId, CancellationToken cancellationToken)
    {
        const string sql = """
            IF NOT EXISTS
            (
                SELECT 1 FROM pcm.Match WITH (UPDLOCK, HOLDLOCK)
                WHERE MatchId = @MatchId AND OrganizerMemberId = @OrganizerMemberId AND Visibility = 'Private'
            )
                THROW 51010, 'The private match is no longer managed by this organizer.', 1;

            INSERT INTO pcm.MatchParticipant (MatchId, MemberId, IsOrganizer, ParticipationStatus)
            VALUES (@MatchId, @ParticipantMemberId, 0, 'Pending');
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
            command.Parameters.Add("@OrganizerMemberId", SqlDbType.Int).Value = organizerMemberId;
            command.Parameters.Add("@ParticipantMemberId", SqlDbType.Int).Value = participantMemberId;
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627 or 51003 or 51010)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new ReservationConflictException("The participant cannot be added to this private match.");
        }
    }

    public async Task<IReadOnlyList<PublicMatch>> GetPublicMatchesAsync(DateTime now, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT m.MatchId, c.CourtId, c.Name, c.SiteId, m.StartsAt, m.EndsAt,
                   4 - COUNT(p.MatchParticipantId) AS AvailablePlaces
            FROM pcm.Match AS m
            INNER JOIN pcm.Court AS c ON c.CourtId = m.CourtId
            LEFT JOIN pcm.MatchParticipant AS p ON p.MatchId = m.MatchId AND p.ParticipationStatus <> 'Removed'
            WHERE m.Visibility = 'Public' AND m.StartsAt > @Now
            GROUP BY m.MatchId, c.CourtId, c.Name, c.SiteId, m.StartsAt, m.EndsAt
            HAVING COUNT(p.MatchParticipantId) < 4
            ORDER BY m.StartsAt, c.Name;
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var matches = new List<PublicMatch>();
        while (await reader.ReadAsync(cancellationToken))
        {
            matches.Add(new PublicMatch(
                reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetInt32(3),
                reader.GetDateTime(4), reader.GetDateTime(5), reader.GetInt32(6)));
        }

        return matches;
    }

    public async Task<PublicMatchJoinResult> JoinPublicMatchAsync(int matchId, int memberId, DateTime paidAt, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            const string validateSql = """
                IF NOT EXISTS
                (
                    SELECT 1 FROM pcm.Match WITH (UPDLOCK, HOLDLOCK)
                    WHERE MatchId = @MatchId AND Visibility = 'Public' AND StartsAt > @PaidAt
                )
                    THROW 51011, 'The public match is unavailable.', 1;
                IF EXISTS (SELECT 1 FROM pcm.MatchParticipant WHERE MatchId = @MatchId AND MemberId = @MemberId)
                    THROW 51012, 'The member already participates in this match.', 1;
                IF (SELECT COUNT(*) FROM pcm.MatchParticipant WITH (UPDLOCK, HOLDLOCK) WHERE MatchId = @MatchId AND ParticipationStatus <> 'Removed') >= 4
                    THROW 51013, 'The public match is full.', 1;
                """;
            await using (var validate = new SqlCommand(validateSql, connection, transaction))
            {
                validate.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
                validate.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
                validate.Parameters.Add("@PaidAt", SqlDbType.DateTime2).Value = paidAt;
                await validate.ExecuteNonQueryAsync(cancellationToken);
            }

            const string joinSql = """
                DECLARE @DebtAmount DECIMAL(9, 2) =
                (
                    SELECT COALESCE(SUM(OutstandingAmount), 0)
                    FROM pcm.Debt WITH (UPDLOCK, HOLDLOCK)
                    WHERE OrganizerMemberId = @MemberId AND OutstandingAmount > 0
                );
                DECLARE @PaymentId INT;
                DECLARE @ParticipantId INT;
                INSERT INTO pcm.Payment (PayerMemberId, Amount, PaymentStatus, PaidAt)
                VALUES (@MemberId, 15.00 + @DebtAmount, 'Succeeded', @PaidAt);
                SET @PaymentId = CONVERT(INT, SCOPE_IDENTITY());
                INSERT INTO pcm.MatchParticipant (MatchId, MemberId, IsOrganizer, ParticipationStatus)
                VALUES (@MatchId, @MemberId, 0, 'Confirmed');
                SET @ParticipantId = CONVERT(INT, SCOPE_IDENTITY());
                INSERT INTO pcm.PaymentAllocation (PaymentId, MatchParticipantId, DebtId, Amount)
                VALUES (@PaymentId, @ParticipantId, NULL, 15.00);
                INSERT INTO pcm.PaymentAllocation (PaymentId, DebtId, Amount)
                SELECT @PaymentId, DebtId, OutstandingAmount
                FROM pcm.Debt
                WHERE OrganizerMemberId = @MemberId AND OutstandingAmount > 0;
                UPDATE pcm.Debt
                SET OutstandingAmount = 0, SettledAt = @PaidAt
                WHERE OrganizerMemberId = @MemberId AND OutstandingAmount > 0;
                SELECT @ParticipantId, @PaymentId;
                """;
            await using var join = new SqlCommand(joinSql, connection, transaction);
            join.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
            join.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
            join.Parameters.Add("@PaidAt", SqlDbType.DateTime2).Value = paidAt;
            PublicMatchJoinResult result;
            await using (var reader = await join.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException("The public match payment was not recorded.");
                }

                result = new PublicMatchJoinResult(matchId, reader.GetInt32(0), reader.GetInt32(1));
            }

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627 or 51003 or 51011 or 51012 or 51013)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new ReservationConflictException("The public place is no longer available.");
        }
    }

    private static MembershipCategory Category(string value) => value switch
    {
        "G" => MembershipCategory.Global,
        "S" => MembershipCategory.Site,
        "L" => MembershipCategory.Free,
        _ => throw new InvalidOperationException("The database contains an unknown member category.")
    };

    private static ReservationVisibility Visibility(string value) => value switch
    {
        "Private" => ReservationVisibility.Private,
        "Public" => ReservationVisibility.Public,
        _ => throw new InvalidOperationException("The database contains an unknown match visibility.")
    };
}
