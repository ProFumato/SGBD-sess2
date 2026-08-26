using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

public interface IAvailabilityService
{
    Task<IReadOnlyList<AvailableSlot>> GetAvailabilityAsync(
        AvailabilityRequest request,
        CancellationToken cancellationToken);
    Task<ReservationResult> CreateReservationAsync(
        ReservationRequest request,
        CancellationToken cancellationToken);
}
