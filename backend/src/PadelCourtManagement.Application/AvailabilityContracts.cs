using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

public interface IAvailabilityService
{
    IReadOnlyList<AvailableSlot> GetAvailability(AvailabilityRequest request);
    ReservationResult CreateReservation(ReservationRequest request);
}
