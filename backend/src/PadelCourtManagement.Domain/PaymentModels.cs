namespace PadelCourtManagement.Domain;

public sealed record PaymentResult(
    int PaymentId,
    int MatchId,
    int MatchParticipantId,
    decimal ParticipantAmount,
    decimal DebtAmount,
    decimal TotalAmount);
