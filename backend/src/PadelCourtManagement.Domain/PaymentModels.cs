// Payment Models: Data types for payment records and outcomes.
// Tracks payment success/failure, amounts, and debt status.

namespace PadelCourtManagement.Domain;

public enum PaymentOutcome
{
    Succeeded,
    Failed
}

public sealed record PaymentResult(
    int PaymentId,
    int MatchId,
    int MatchParticipantId,
    decimal ParticipantAmount,
    decimal DebtAmount,
    decimal TotalAmount,
    PaymentOutcome Outcome = PaymentOutcome.Succeeded);
