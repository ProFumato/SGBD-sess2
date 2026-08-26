using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

public interface IAvailabilityRepository
{
    IReadOnlyList<AvailableSlot> GetAvailability(AvailabilityRequest request);
    ReservationResult CreateReservation(ReservationRequest request);
}
