using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

public sealed class AvailabilityService : IAvailabilityService
{
    private readonly IAvailabilityRepository repository;

    public AvailabilityService(IAvailabilityRepository repository)
    {
        this.repository = repository;
    }

    public IReadOnlyList<AvailableSlot> GetAvailability(AvailabilityRequest request)
        => repository.GetAvailability(request);

    public ReservationResult CreateReservation(ReservationRequest request)
        => repository.CreateReservation(request);
}
