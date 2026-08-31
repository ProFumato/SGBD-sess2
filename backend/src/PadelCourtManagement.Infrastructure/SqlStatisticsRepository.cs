using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PadelCourtManagement.Application;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Infrastructure;

public sealed class SqlStatisticsRepository(IConfiguration configuration) : IStatisticsRepository
{
    private readonly string connectionString = configuration.GetConnectionString("PadelCourtManagement")
        ?? throw new InvalidOperationException("Missing connection string 'PadelCourtManagement'.");

    public async Task<StatisticsReport> GetAsync(
        StatisticsRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        const string summarySql = """
            SELECT
                COALESCE((
                    SELECT SUM(allocation.Amount)
                    FROM pcm.PaymentAllocation AS allocation
                    LEFT JOIN pcm.MatchParticipant AS participant
                        ON participant.MatchParticipantId = allocation.MatchParticipantId
                    LEFT JOIN pcm.Match AS participantMatch
                        ON participantMatch.MatchId = participant.MatchId
                    LEFT JOIN pcm.Debt AS debt
                        ON debt.DebtId = allocation.DebtId
                    LEFT JOIN pcm.Match AS debtMatch
                        ON debtMatch.MatchId = debt.MatchId
                    WHERE COALESCE(participantMatch.StartsAt, debtMatch.StartsAt) >= @From
                      AND COALESCE(participantMatch.StartsAt, debtMatch.StartsAt) < @To
                      AND (@SiteId IS NULL OR COALESCE(participantMatch.CourtId, debtMatch.CourtId) IN
                          (SELECT CourtId FROM pcm.Court WHERE SiteId = @SiteId))
                ), 0.00) AS Revenue,
                COUNT(DISTINCT match.MatchId) AS Matches,
                COALESCE(SUM(CASE WHEN participant.ParticipationStatus = 'Confirmed' THEN 1 ELSE 0 END), 0) AS ConfirmedParticipations,
                COUNT(DISTINCT match.MatchId) * 4 AS Capacity,
                (SELECT COUNT(*) FROM pcm.Member WHERE IsActive = 1) AS ActiveMembers,
                COALESCE((
                    SELECT SUM(debt.OutstandingAmount)
                    FROM pcm.Debt AS debt
                    INNER JOIN pcm.Match AS debtMatch ON debtMatch.MatchId = debt.MatchId
                    INNER JOIN pcm.Court AS debtCourt ON debtCourt.CourtId = debtMatch.CourtId
                    WHERE debt.OutstandingAmount > 0
                      AND debtMatch.StartsAt >= @From
                      AND debtMatch.StartsAt < @To
                      AND (@SiteId IS NULL OR debtCourt.SiteId = @SiteId)
                ), 0.00) AS OutstandingDebt,
                (SELECT COUNT(*)
                 FROM pcm.BookingBan AS ban
                 INNER JOIN pcm.Match AS banMatch ON banMatch.MatchId = ban.SourceMatchId
                 INNER JOIN pcm.Court AS banCourt ON banCourt.CourtId = banMatch.CourtId
                 WHERE ban.EndsAt > SYSUTCDATETIME()
                   AND (@SiteId IS NULL OR banCourt.SiteId = @SiteId)) AS ActiveBookingBans
            FROM pcm.Match AS match
            INNER JOIN pcm.Court AS court ON court.CourtId = match.CourtId
            LEFT JOIN pcm.MatchParticipant AS participant
                ON participant.MatchId = match.MatchId
               AND participant.ParticipationStatus <> 'Removed'
            WHERE match.StartsAt >= @From
              AND match.StartsAt < @To
              AND (@SiteId IS NULL OR court.SiteId = @SiteId);
            """;

        await using var summaryCommand = CreateCommand(connection, summarySql, request);
        await using var summaryReader = await summaryCommand.ExecuteReaderAsync(cancellationToken);
        await summaryReader.ReadAsync(cancellationToken);
        var report = new StatisticsReport(
            request.From,
            request.To,
            summaryReader.GetDecimal(0),
            summaryReader.GetInt32(1),
            summaryReader.GetInt32(2),
            summaryReader.GetInt32(3),
            summaryReader.GetInt32(4),
            summaryReader.GetDecimal(5),
            summaryReader.GetInt32(6),
            []);
        await summaryReader.DisposeAsync();

        const string breakdownSql = """
            WITH MatchStats AS
            (
                SELECT match.CourtId,
                       COUNT(*) AS Matches,
                       SUM(CASE WHEN participant.ParticipationStatus = 'Confirmed' THEN 1 ELSE 0 END) AS ConfirmedParticipations
                FROM pcm.Match AS match
                LEFT JOIN pcm.MatchParticipant AS participant
                    ON participant.MatchId = match.MatchId
                   AND participant.ParticipationStatus <> 'Removed'
                WHERE match.StartsAt >= @From
                  AND match.StartsAt < @To
                GROUP BY match.CourtId
            ),
            Revenue AS
            (
                SELECT COALESCE(participantMatch.CourtId, debtMatch.CourtId) AS CourtId,
                       SUM(allocation.Amount) AS Revenue
                FROM pcm.PaymentAllocation AS allocation
                LEFT JOIN pcm.MatchParticipant AS participant
                    ON participant.MatchParticipantId = allocation.MatchParticipantId
                LEFT JOIN pcm.Match AS participantMatch
                    ON participantMatch.MatchId = participant.MatchId
                LEFT JOIN pcm.Debt AS debt
                    ON debt.DebtId = allocation.DebtId
                LEFT JOIN pcm.Match AS debtMatch
                    ON debtMatch.MatchId = debt.MatchId
                WHERE COALESCE(participantMatch.StartsAt, debtMatch.StartsAt) >= @From
                  AND COALESCE(participantMatch.StartsAt, debtMatch.StartsAt) < @To
                GROUP BY COALESCE(participantMatch.CourtId, debtMatch.CourtId)
            )
            SELECT site.SiteId, site.Name, court.CourtId, court.Name,
                   COALESCE(stats.Matches, 0),
                   COALESCE(stats.ConfirmedParticipations, 0),
                   COALESCE(revenue.Revenue, 0.00)
            FROM pcm.Site AS site
            INNER JOIN pcm.Court AS court ON court.SiteId = site.SiteId
            LEFT JOIN MatchStats AS stats ON stats.CourtId = court.CourtId
            LEFT JOIN Revenue AS revenue ON revenue.CourtId = court.CourtId
            WHERE (@SiteId IS NULL OR site.SiteId = @SiteId)
            ORDER BY site.Name, court.Name;
            """;

        var breakdown = new List<StatisticsBreakdown>();
        await using var breakdownCommand = CreateCommand(connection, breakdownSql, request);
        await using var breakdownReader = await breakdownCommand.ExecuteReaderAsync(cancellationToken);
        while (await breakdownReader.ReadAsync(cancellationToken))
        {
            breakdown.Add(new StatisticsBreakdown(
                breakdownReader.GetInt32(0),
                breakdownReader.GetString(1),
                breakdownReader.GetInt32(2),
                breakdownReader.GetString(3),
                breakdownReader.GetInt32(4),
                breakdownReader.GetInt32(5),
                breakdownReader.GetDecimal(6)));
        }

        return report with { Breakdown = breakdown };
    }

    private static SqlCommand CreateCommand(
        SqlConnection connection,
        string sql,
        StatisticsRequest request)
    {
        var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@From", SqlDbType.DateTime2).Value = request.From;
        command.Parameters.Add("@To", SqlDbType.DateTime2).Value = request.To;
        command.Parameters.Add("@SiteId", SqlDbType.Int).Value = request.SiteId ?? (object)DBNull.Value;
        return command;
    }
}
