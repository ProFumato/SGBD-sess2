// Payment Service Contract: Interface definition.
// Defines method for recording payments and updating member balances.

using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

public interface IPaymentService
{
    Task<PaymentResult> PayParticipantAsync(
        int matchId,
        string matricule,
        CancellationToken cancellationToken,
        PaymentOutcome outcome = PaymentOutcome.Succeeded);
}

public interface IPaymentRepository
{
    Task<ReservationMember?> GetMemberAsync(string matricule, CancellationToken cancellationToken);
    Task<PaymentResult> PayParticipantAsync(
        int matchId,
        int memberId,
        DateTime paidAt,
        CancellationToken cancellationToken,
        PaymentOutcome outcome = PaymentOutcome.Succeeded);
}
