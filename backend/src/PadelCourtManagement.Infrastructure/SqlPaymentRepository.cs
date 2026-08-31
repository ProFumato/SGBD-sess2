using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PadelCourtManagement.Application;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Infrastructure;

public sealed class SqlPaymentRepository(IConfiguration configuration) : IPaymentRepository
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
                Category(reader.GetString(1)),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                reader.GetBoolean(3))
            : null;
    }

    public async Task<PaymentResult> PayParticipantAsync(
        int matchId,
        int memberId,
        DateTime paidAt,
        CancellationToken cancellationToken,
        PaymentOutcome outcome = PaymentOutcome.Succeeded)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

        try
        {
            const string participantSql = """
                SELECT p.MatchParticipantId, p.MemberId, m.OrganizerMemberId,
                       p.ParticipationStatus, m.StartsAt, m.Visibility
                FROM pcm.MatchParticipant AS p WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN pcm.Match AS m WITH (UPDLOCK, HOLDLOCK) ON m.MatchId = p.MatchId
                WHERE p.MatchId = @MatchId AND p.MemberId = @MemberId;
                """;

            int participantId;
            int organizerId;
            string status;
            DateTime startsAt;
            string visibility;
            await using (var participant = new SqlCommand(participantSql, connection, transaction))
            {
                participant.Parameters.Add("@MatchId", SqlDbType.Int).Value = matchId;
                participant.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
                await using var reader = await participant.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new ReservationNotFoundException("The member is not a participant in this match.");
                }

                participantId = reader.GetInt32(0);
                organizerId = reader.GetInt32(2);
                status = reader.GetString(3);
                startsAt = reader.GetDateTime(4);
                visibility = reader.GetString(5);
            }

            if (!string.Equals(status, "Pending", StringComparison.Ordinal))
            {
                throw new ReservationConflictException("This participant place is not awaiting payment.");
            }

            if (startsAt <= paidAt)
            {
                throw new ReservationConflictException("The match can no longer be paid.");
            }

            var debtsToSettle = new List<(int Id, decimal Amount)>();
            decimal debtAmount = 0m;
            if (outcome == PaymentOutcome.Succeeded && memberId == organizerId)
            {
                const string debtsSql = """
                    SELECT DebtId, OutstandingAmount
                    FROM pcm.Debt WITH (UPDLOCK, HOLDLOCK)
                    WHERE OrganizerMemberId = @MemberId AND OutstandingAmount > 0
                    ORDER BY DebtId;
                    """;
                await using var debts = new SqlCommand(debtsSql, connection, transaction);
                debts.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
                await using var debtReader = await debts.ExecuteReaderAsync(cancellationToken);
                while (await debtReader.ReadAsync(cancellationToken))
                {
                    var amount = debtReader.GetDecimal(1);
                    debtsToSettle.Add((debtReader.GetInt32(0), amount));
                    debtAmount += amount;
                }
            }

            const decimal participantAmount = 15.00m;
            debtAmount = Math.Min(debtAmount, participantAmount);
            var totalAmount = participantAmount;
            int paymentId;
            const string paymentSql = """
                INSERT INTO pcm.Payment (PayerMemberId, Amount, PaymentStatus, PaidAt)
                OUTPUT INSERTED.PaymentId
                VALUES (@MemberId, @Amount, @Status, @PaidAt);
                """;
            await using (var payment = new SqlCommand(paymentSql, connection, transaction))
            {
                payment.Parameters.Add("@MemberId", SqlDbType.Int).Value = memberId;
                payment.Parameters.Add("@Amount", SqlDbType.Decimal).Value = totalAmount;
                payment.Parameters.Add("@Status", SqlDbType.VarChar).Value =
                    outcome == PaymentOutcome.Succeeded ? "Succeeded" : "Failed";
                payment.Parameters.Add("@PaidAt", SqlDbType.DateTime2).Value = paidAt;
                if (outcome == PaymentOutcome.Failed)
                {
                    payment.Parameters["@PaidAt"].Value = DBNull.Value;
                }
                payment.Parameters["@Amount"].Precision = 9;
                payment.Parameters["@Amount"].Scale = 2;
                paymentId = Convert.ToInt32(await payment.ExecuteScalarAsync(cancellationToken));
            }

            if (outcome == PaymentOutcome.Succeeded)
            {
                const string participantAllocationSql = """
                    INSERT INTO pcm.PaymentAllocation (PaymentId, MatchParticipantId, Amount)
                    VALUES (@PaymentId, @ParticipantId, @Amount);
                    UPDATE pcm.MatchParticipant
                    SET ParticipationStatus = 'Confirmed'
                    WHERE MatchParticipantId = @ParticipantId;
                    """;
                await using (var allocation = new SqlCommand(participantAllocationSql, connection, transaction))
                {
                    allocation.Parameters.Add("@PaymentId", SqlDbType.Int).Value = paymentId;
                    allocation.Parameters.Add("@ParticipantId", SqlDbType.Int).Value = participantId;
                    allocation.Parameters.Add("@Amount", SqlDbType.Decimal).Value = participantAmount;
                    allocation.Parameters["@Amount"].Precision = 9;
                    allocation.Parameters["@Amount"].Scale = 2;
                    await allocation.ExecuteNonQueryAsync(cancellationToken);
                }

                var remainingDebtPayment = participantAmount;
                foreach (var debtEntry in debtsToSettle)
                {
                    if (remainingDebtPayment <= 0) break;
                    const string debtSql = """
                        DECLARE @OutstandingAmount DECIMAL(9, 2);
                        SELECT @OutstandingAmount = OutstandingAmount
                        FROM pcm.Debt WITH (UPDLOCK, HOLDLOCK)
                        WHERE DebtId = @DebtId AND OutstandingAmount > 0;
                        DECLARE @AppliedAmount DECIMAL(9, 2) =
                            CASE WHEN @OutstandingAmount < @RemainingAmount
                                 THEN @OutstandingAmount ELSE @RemainingAmount END;
                        IF @AppliedAmount > 0
                        BEGIN
                            INSERT INTO pcm.PaymentAllocation (PaymentId, DebtId, Amount)
                            VALUES (@PaymentId, @DebtId, @AppliedAmount);
                        END;
                        UPDATE pcm.Debt
                        SET OutstandingAmount = OutstandingAmount - @AppliedAmount,
                            SettledAt = CASE WHEN OutstandingAmount - @AppliedAmount = 0 THEN @PaidAt ELSE NULL END
                        WHERE DebtId = @DebtId AND OutstandingAmount > 0;
                        SET @RemainingAmount -= @AppliedAmount;
                        """;
                    await using var debtCommand = new SqlCommand(debtSql, connection, transaction);
                    debtCommand.Parameters.Add("@PaymentId", SqlDbType.Int).Value = paymentId;
                    debtCommand.Parameters.Add("@DebtId", SqlDbType.Int).Value = debtEntry.Id;
                    debtCommand.Parameters.Add("@PaidAt", SqlDbType.DateTime2).Value = paidAt;
                    debtCommand.Parameters.Add("@RemainingAmount", SqlDbType.Decimal).Value = remainingDebtPayment;
                    debtCommand.Parameters["@RemainingAmount"].Precision = 9;
                    debtCommand.Parameters["@RemainingAmount"].Scale = 2;
                    await debtCommand.ExecuteNonQueryAsync(cancellationToken);
                    remainingDebtPayment -= Math.Min(remainingDebtPayment, debtEntry.Amount);
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return new PaymentResult(paymentId, matchId, participantId, participantAmount, debtAmount, totalAmount, outcome);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static MembershipCategory Category(string value) => value switch
    {
        "G" => MembershipCategory.Global,
        "S" => MembershipCategory.Site,
        "L" => MembershipCategory.Free,
        _ => throw new InvalidOperationException("The database contains an unknown member category.")
    };
}
