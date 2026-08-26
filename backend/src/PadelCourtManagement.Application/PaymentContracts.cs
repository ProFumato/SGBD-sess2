using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

public interface IPaymentService
{
    Task<PaymentResult> PayParticipantAsync(
        int matchId,
        string matricule,
        CancellationToken cancellationToken);
}

public interface IPaymentRepository
{
    Task<ReservationMember?> GetMemberAsync(string matricule, CancellationToken cancellationToken);
    Task<PaymentResult> PayParticipantAsync(
        int matchId,
        int memberId,
        DateTime paidAt,
        CancellationToken cancellationToken);
}
