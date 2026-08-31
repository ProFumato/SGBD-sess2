using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

public interface IAvailabilityRepository
{
    Task<ReservationMember?> GetMemberAsync(string matricule, CancellationToken cancellationToken);
    Task<IReadOnlyList<AvailableSlot>> GetAvailabilityAsync(
        int siteId,
        DateOnly date,
        CancellationToken cancellationToken);
    Task<ReservationContext?> GetReservationContextAsync(
        string matricule,
        int courtId,
        DateTime startAt,
        DateTime now,
        CancellationToken cancellationToken);
    Task<ReservationResult> CreateReservationAsync(
        ReservationCommand command,
        CancellationToken cancellationToken);
}
