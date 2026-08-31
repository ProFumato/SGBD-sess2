using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PadelCourtManagement.Application;
using PadelCourtManagement.Application.Administration;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Infrastructure;

public sealed class SqlDebtRepository(IConfiguration configuration) : IDebtRepository
{
    private readonly string connectionString = configuration.GetConnectionString("PadelCourtManagement")
        ?? throw new InvalidOperationException("Missing connection string 'PadelCourtManagement'.");

    public async Task<ReservationMember?> GetMemberAsync(string matricule, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT MemberId, MembershipCategory, HomeSiteId, IsActive
            FROM pcm.Member
            WHERE Matricule = @Matricule;
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Matricule", SqlDbType.VarChar).Value = matricule;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ReservationMember(
                reader.GetInt32(0),
                reader.GetString(1) switch
                {
                    "G" => MembershipCategory.Global,
                    "S" => MembershipCategory.Site,
                    "L" => MembershipCategory.Free,
                    _ => throw new InvalidOperationException("The database contains an unknown member category.")
                },
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.GetBoolean(3))
            : null;
    }

    public async Task<IReadOnlyList<MemberDebt>> GetOutstandingDebtsAsync(
        int memberId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT debt.DebtId, debt.MatchId, court.Name, court.SiteId,
                   match.StartsAt, debt.InitialAmount, debt.OutstandingAmount
            FROM pcm.Debt AS debt
            INNER JOIN pcm.Match AS match ON match.MatchId = debt.MatchId
            INNER JOIN pcm.Court AS court ON court.CourtId = match.CourtId
            WHERE debt.OrganizerMemberId = @MemberId
              AND debt.OutstandingAmount > 0
            ORDER BY match.StartsAt, debt.DebtId;
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var debts = new List<MemberDebt>();
        while (await reader.ReadAsync(cancellationToken))
        {
            debts.Add(new MemberDebt(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetDateTime(4),
                reader.GetDecimal(5),
                reader.GetDecimal(6)));
        }

        return debts;
    }

    public Task<IReadOnlyList<MemberDebt>> GetDebtsForAdministratorAsync(
        int memberId,
        AdministratorScope scope,
        int? siteId,
        CancellationToken cancellationToken) =>
        GetDebtsAsync(memberId, scope, siteId, cancellationToken);

    public async Task ClearDebtsForAdministratorAsync(
        int memberId,
        AdministratorScope scope,
        int? siteId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE debt
            SET OutstandingAmount = 0, SettledAt = SYSUTCDATETIME()
            FROM pcm.Debt AS debt
            INNER JOIN pcm.Match AS match ON match.MatchId = debt.MatchId
            INNER JOIN pcm.Court AS court ON court.CourtId = match.CourtId
            WHERE debt.OrganizerMemberId = @MemberId
              AND debt.OutstandingAmount > 0
              AND (@Scope = 'Global' OR court.SiteId = @SiteId);
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
        command.Parameters.Add("@Scope", SqlDbType.VarChar).Value = scope.ToString();
        command.Parameters.Add("@SiteId", SqlDbType.Int).Value = (object?)siteId ?? DBNull.Value;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<MemberDebt>> GetDebtsAsync(
        int memberId,
        AdministratorScope scope,
        int? siteId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT debt.DebtId, debt.MatchId, court.Name, court.SiteId,
                   match.StartsAt, debt.InitialAmount, debt.OutstandingAmount
            FROM pcm.Debt AS debt
            INNER JOIN pcm.Match AS match ON match.MatchId = debt.MatchId
            INNER JOIN pcm.Court AS court ON court.CourtId = match.CourtId
            WHERE debt.OrganizerMemberId = @MemberId
              AND (@Scope = 'Global' OR court.SiteId = @SiteId)
            ORDER BY match.StartsAt, debt.DebtId;
            """;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
        command.Parameters.Add("@Scope", SqlDbType.VarChar).Value = scope.ToString();
        command.Parameters.Add("@SiteId", SqlDbType.Int).Value = (object?)siteId ?? DBNull.Value;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var debts = new List<MemberDebt>();
        while (await reader.ReadAsync(cancellationToken))
        {
            debts.Add(new MemberDebt(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2),
                reader.GetInt32(3), reader.GetDateTime(4), reader.GetDecimal(5), reader.GetDecimal(6)));
        }
        return debts;
    }
}
