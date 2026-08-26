using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

public sealed class PaymentService(IPaymentRepository repository) : IPaymentService
{
    public async Task<PaymentResult> PayParticipantAsync(
        int matchId,
        string matricule,
        CancellationToken cancellationToken,
        PaymentOutcome outcome = PaymentOutcome.Succeeded)
    {
        if (matchId <= 0)
        {
            throw new ReservationValidationException("A valid match identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(matricule))
        {
            throw new ReservationValidationException("The matricule is required.");
        }

        var member = await repository.GetMemberAsync(
            matricule.Trim().ToUpperInvariant(),
            cancellationToken)
            ?? throw new ReservationNotFoundException("The matricule does not identify a member.");
        if (!member.IsActive)
        {
            throw new ReservationForbiddenException("Inactive members cannot make payments.");
        }

        return await repository.PayParticipantAsync(
            matchId,
            member.MemberId,
            DateTime.UtcNow,
            cancellationToken,
            outcome);
    }
}
