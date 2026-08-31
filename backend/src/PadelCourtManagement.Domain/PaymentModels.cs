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
