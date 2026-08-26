using PadelCourtManagement.Application;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Tests;

public sealed class AvailabilityServiceTests
{
    [Fact]
    public void GetAvailabilityReturnsASlotForFutureRequests()
    {
        var service = new AvailabilityService(new FakeAvailabilityRepository());
        var request = new AvailabilityRequest("G0001", "S1", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), new TimeOnly(10, 0));

        var result = service.GetAvailability(request);

        Assert.Single(result);
    }

    [Fact]
    public void CreateReservationReturnsReservationData()
    {
        var service = new AvailabilityService(new FakeAvailabilityRepository());
        var start = new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.FromHours(2));

        var result = service.CreateReservation(new ReservationRequest("G0001", "S1-C1", start, ReservationVisibility.Public));

        Assert.Equal("R0001", result.ReservationCode);
        Assert.Equal("S1-C1", result.CourtCode);
    }

    private sealed class FakeAvailabilityRepository : IAvailabilityRepository
    {
        public IReadOnlyList<AvailableSlot> GetAvailability(AvailabilityRequest request)
            => [new AvailableSlot("S1-C1", DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(1).AddMinutes(90))];

        public ReservationResult CreateReservation(ReservationRequest request)
            => new("R0001", request.CourtCode, request.Start, request.Start.AddMinutes(90));
    }
}
