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

    public async Task<IReadOnlyList<MatchParticipantDetails>> GetPrivateParticipantsAsync(
        int matchId,
        int memberId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT p.MatchParticipantId, p.MemberId, member.Matricule, member.DisplayName,
                   p.IsOrganizer, p.ParticipationStatus,
                   CAST(CASE WHEN EXISTS
                   (
                       SELECT 1
                       FROM pcm.PaymentAllocation AS allocation
                       WHERE allocation.MatchParticipantId = p.MatchParticipantId
                   ) THEN 1 ELSE 0 END AS bit)
            FROM pcm.MatchParticipant AS p
            INNER JOIN pcm.Member AS member ON member.MemberId = p.MemberId
            INNER JOIN pcm.Match AS match ON match.MatchId = p.MatchId
            WHERE p.MatchId = @MatchId
              AND EXISTS
              (
                  SELECT 1
                  FROM pcm.MatchParticipant AS viewer
                  WHERE viewer.MatchId = match.MatchId
                    AND viewer.MemberId = @MemberId
                    AND viewer.ParticipationStatus <> 'Removed'
              )
              AND match.Visibility = 'Private'
            ORDER BY p.IsOrganizer DESC, p.AddedAt;
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
        command.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var participants = new List<MatchParticipantDetails>();
        while (await reader.ReadAsync(cancellationToken))
        {
            participants.Add(ReadParticipant(reader, 0));
        }

        return participants;
    }

    public async Task<IReadOnlyList<PrivateMatchOverview>> GetPrivateMatchesAsync(
        int memberId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT match.MatchId, court.CourtId, court.Name, site.SiteId, site.Name,
                   match.StartsAt, match.EndsAt,
                   p.MatchParticipantId, p.MemberId, member.Matricule, member.DisplayName,
                   p.IsOrganizer, p.ParticipationStatus,
                   CAST(CASE WHEN EXISTS
                   (
                       SELECT 1 FROM pcm.PaymentAllocation AS allocation
                       WHERE allocation.MatchParticipantId = p.MatchParticipantId
                   ) THEN 1 ELSE 0 END AS bit)
            FROM pcm.Match AS match
            INNER JOIN pcm.Court AS court ON court.CourtId = match.CourtId
            INNER JOIN pcm.Site AS site ON site.SiteId = court.SiteId
            INNER JOIN pcm.MatchParticipant AS viewer
                ON viewer.MatchId = match.MatchId
               AND viewer.MemberId = @MemberId
               AND viewer.ParticipationStatus <> 'Removed'
            INNER JOIN pcm.MatchParticipant AS p ON p.MatchId = match.MatchId
            INNER JOIN pcm.Member AS member ON member.MemberId = p.MemberId
            WHERE match.Visibility = 'Private'
              AND match.StartsAt > @Now
            ORDER BY match.StartsAt, match.MatchId, p.IsOrganizer DESC, p.AddedAt;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
        command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var matches = new List<PrivateMatchOverview>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var matchId = reader.GetInt32(0);
            var participants = matches.LastOrDefault()?.MatchId == matchId
                ? matches[^1].Participants.ToList()
                : [];
            participants.Add(ReadParticipant(reader, 7));
            var overview = new PrivateMatchOverview(
                matchId,
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetDateTime(5),
                reader.GetDateTime(6),
                participants);
            if (matches.LastOrDefault()?.MatchId == matchId)
            {
                matches[^1] = overview;
            }
            else
            {
                matches.Add(overview);
            }
        }

        return matches;
    }

    public async Task RemovePrivateParticipantAsync(
        int matchId,
        int participantId,
        int organizerMemberId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE participant
            SET ParticipationStatus = 'Removed'
            FROM pcm.MatchParticipant AS participant
            INNER JOIN pcm.Match AS match ON match.MatchId = participant.MatchId
            WHERE participant.MatchParticipantId = @ParticipantId
              AND participant.MatchId = @MatchId
              AND participant.IsOrganizer = 0
              AND participant.ParticipationStatus = 'Pending'
              AND match.OrganizerMemberId = @OrganizerMemberId
              AND match.Visibility = 'Private'
              AND match.StartsAt > @Now;
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@ParticipantId", SqlDbType.Int).Value = participantId;
        command.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
        command.Parameters.Add("@OrganizerMemberId", SqlDbType.Int).Value = organizerMemberId;
        command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new ReservationConflictException(
                "Only a pending non-organizer participant can be removed before the match.");
        }
    }

    public async Task ReplacePrivateParticipantAsync(
        int matchId,
        int participantId,
        int organizerMemberId,
        int replacementMemberId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE participant
            SET MemberId = @ReplacementMemberId,
                ParticipationStatus = 'Pending',
                AddedAt = SYSUTCDATETIME()
            FROM pcm.MatchParticipant AS participant
            INNER JOIN pcm.Match AS match ON match.MatchId = participant.MatchId
            WHERE participant.MatchParticipantId = @ParticipantId
              AND participant.MatchId = @MatchId
              AND participant.IsOrganizer = 0
              AND participant.ParticipationStatus = 'Pending'
              AND match.OrganizerMemberId = @OrganizerMemberId
              AND match.Visibility = 'Private'
              AND match.StartsAt > @Now;
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@ParticipantId", SqlDbType.Int).Value = participantId;
        command.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
        command.Parameters.Add("@OrganizerMemberId", SqlDbType.Int).Value = organizerMemberId;
        command.Parameters.Add("@ReplacementMemberId", SqlDbType.Int).Value = replacementMemberId;
        command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
        try
        {
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new ReservationConflictException(
                    "Only a pending non-organizer participant can be replaced before the match.");
            }
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new ReservationConflictException("The replacement member already participates in this match.");
        }
    }

    public async Task<IReadOnlyList<PublicMatch>> GetPublicMatchesAsync(int memberId, DateTime now, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT public_match.MatchId, public_match.CourtId, public_match.CourtName, public_match.SiteId,
                   public_match.StartsAt, public_match.EndsAt, public_match.AvailablePlaces,
                   member.MemberId, member.Matricule, member.DisplayName
            FROM
            (
                SELECT m.MatchId, c.CourtId, c.Name AS CourtName, c.SiteId, m.StartsAt, m.EndsAt,
                       4 - COUNT(p.MatchParticipantId) AS AvailablePlaces
                FROM pcm.Match AS m
                INNER JOIN pcm.Court AS c ON c.CourtId = m.CourtId
                LEFT JOIN pcm.MatchParticipant AS p ON p.MatchId = m.MatchId AND p.ParticipationStatus <> 'Removed'
                WHERE m.Visibility = 'Public' AND m.StartsAt > @Now
                GROUP BY m.MatchId, c.CourtId, c.Name, c.SiteId, m.StartsAt, m.EndsAt
            ) AS public_match
            LEFT JOIN pcm.MatchParticipant AS participant
                ON participant.MatchId = public_match.MatchId
               AND participant.ParticipationStatus <> 'Removed'
            LEFT JOIN pcm.Member AS member ON member.MemberId = participant.MemberId
            ORDER BY public_match.StartsAt, public_match.CourtName,
                     CASE WHEN member.MemberId = @MemberId THEN 0 ELSE 1 END,
                     member.DisplayName;
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
        command.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var matches = new List<PublicMatch>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var matchId = reader.GetInt32(0);
            var participants = matches.LastOrDefault()?.MatchId == matchId
                ? matches[^1].Participants.ToList()
                : [];

            if (!reader.IsDBNull(7))
            {
                participants.Add(new PublicMatchParticipant(
                    reader.GetInt32(7),
                    reader.GetString(8),
                    reader.GetString(9)));
            }

            var match = new PublicMatch(
                matchId,
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetDateTime(4),
                reader.GetDateTime(5),
                reader.GetInt32(6),
                participants);

            if (matches.LastOrDefault()?.MatchId == matchId)
            {
                matches[^1] = match;
            }
            else
            {
                matches.Add(match);
            }
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
                    WHERE MatchId = @MatchId
                )
                    THROW 51014, 'The match does not exist.', 1;
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
                DECLARE @PaymentId INT;
                DECLARE @ParticipantId INT;
                DECLARE @DebtOwnerMemberId INT = @MemberId;
                DECLARE @DebtId INT;
                DECLARE @DebtAmount DECIMAL(9, 2);
                DECLARE @RemainingAmount DECIMAL(9, 2) = 0.00;
                INSERT INTO pcm.Payment (PayerMemberId, Amount, PaymentStatus, PaidAt)
                VALUES (@MemberId, 15.00, 'Succeeded', @PaidAt);
                SET @PaymentId = CONVERT(INT, SCOPE_IDENTITY());
                INSERT INTO pcm.MatchParticipant (MatchId, MemberId, IsOrganizer, ParticipationStatus)
                VALUES (@MatchId, @MemberId, 0, 'Confirmed');
                SET @ParticipantId = CONVERT(INT, SCOPE_IDENTITY());
                INSERT INTO pcm.PaymentAllocation (PaymentId, MatchParticipantId, DebtId, Amount)
                VALUES (@PaymentId, @ParticipantId, NULL, 15.00);
                WHILE @RemainingAmount > 0
                BEGIN
                    SELECT TOP (1) @DebtId = DebtId, @DebtAmount = OutstandingAmount
                    FROM pcm.Debt WITH (UPDLOCK, HOLDLOCK)
                    WHERE OrganizerMemberId = @DebtOwnerMemberId AND OutstandingAmount > 0
                    ORDER BY DebtId;
                    IF @DebtId IS NULL BREAK;
                    DECLARE @AppliedAmount DECIMAL(9, 2) =
                        CASE WHEN @DebtAmount < @RemainingAmount THEN @DebtAmount ELSE @RemainingAmount END;
                    INSERT INTO pcm.PaymentAllocation (PaymentId, DebtId, Amount)
                    VALUES (@PaymentId, @DebtId, @AppliedAmount);
                    UPDATE pcm.Debt
                    SET OutstandingAmount = OutstandingAmount - @AppliedAmount,
                        SettledAt = CASE WHEN OutstandingAmount - @AppliedAmount = 0 THEN @PaidAt ELSE NULL END
                    WHERE DebtId = @DebtId;
                    SET @RemainingAmount -= @AppliedAmount;
                    SET @DebtId = NULL;
                END;
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
        catch (SqlException exception) when (exception.Number == 51014)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new ReservationNotFoundException("The match does not exist.");
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

    private static MatchParticipantDetails ReadParticipant(SqlDataReader reader, int offset) =>
        new(
            reader.GetInt32(offset),
            reader.GetInt32(offset + 1),
            reader.GetString(offset + 2),
            reader.GetString(offset + 3),
            reader.GetBoolean(offset + 4),
            reader.GetString(offset + 5),
            reader.GetBoolean(offset + 6));

    private static ReservationVisibility Visibility(string value) => value switch
    {
        "Private" => ReservationVisibility.Private,
        "Public" => ReservationVisibility.Public,
        _ => throw new InvalidOperationException("The database contains an unknown match visibility.")
    };
}
