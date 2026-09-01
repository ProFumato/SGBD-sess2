using PadelCourtManagement.Application;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Tests;

public sealed class AvailabilityServiceTests
{
    [Fact]
    public async Task ActiveGlobalMemberCanCreateEligibleReservation()
    {
        // arrange
        // Simple fake repository: this test checks service rules without SQL Server.
        var repository = new FakeAvailabilityRepository
        {
            Context = CreateContext()
        };
        var service = new AvailabilityService(repository);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

        // act
        var result = await service.CreateReservationAsync(
            new ReservationRequest("g0001", 10, date, new TimeOnly(10, 0), ReservationVisibility.Public),
            CancellationToken.None);

        // assert; the fake records the command received from the service.
        Assert.Equal(42, result.MatchId);
        Assert.Equal(1, repository.CreateCalls);
        Assert.Equal(10, repository.LastCommand!.CourtId);
    }

    [Fact]
    public async Task SiteMemberCannotReserveOutsideHomeSite()
    {
        var repository = new FakeAvailabilityRepository
        {
            Context = CreateContext(member: new ReservationMember(1, MembershipCategory.Site, 2, true), siteId: 3)
        };
        var service = new AvailabilityService(repository);

        await Assert.ThrowsAsync<ReservationForbiddenException>(() =>
            service.CreateReservationAsync(
                new ReservationRequest("S00001", 10, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), new TimeOnly(10, 0), ReservationVisibility.Private),
                CancellationToken.None));
    }

    [Fact]
    public async Task BookingWindowIsAppliedToFreeMembers()
    {
        var repository = new FakeAvailabilityRepository
        {
            Context = CreateContext(member: new ReservationMember(1, MembershipCategory.Free, null, true))
        };
        var service = new AvailabilityService(repository);

        await Assert.ThrowsAsync<ReservationForbiddenException>(() =>
            service.CreateReservationAsync(
                new ReservationRequest("L00001", 10, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), new TimeOnly(10, 0), ReservationVisibility.Private),
                CancellationToken.None));
    }

    [Fact]
    public async Task ActiveDebtBlocksReservationCreation()
    {
        var repository = new FakeAvailabilityRepository
        {
            Context = CreateContext(hasActiveDebt: true)
        };
        var service = new AvailabilityService(repository);

        await Assert.ThrowsAsync<ReservationForbiddenException>(() =>
            service.CreateReservationAsync(
                new ReservationRequest("G0001", 10, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), new TimeOnly(10, 0), ReservationVisibility.Private),
                CancellationToken.None));
    }

    [Fact]
    public async Task InactiveMemberCannotQueryAvailability()
    {
        var repository = new FakeAvailabilityRepository
        {
            Member = new ReservationMember(1, MembershipCategory.Global, null, false)
        };
        var service = new AvailabilityService(repository);

        await Assert.ThrowsAsync<ReservationForbiddenException>(() =>
            service.GetAvailabilityAsync(
                new AvailabilityRequest("G0001", 1, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))),
                CancellationToken.None));
    }

    private static ReservationContext CreateContext(
        ReservationMember? member = null,
        int siteId = 1,
        bool hasActiveDebt = false) =>
        new(
            member ?? new ReservationMember(1, MembershipCategory.Global, null, true),
            10,
            siteId,
            true,
            new TimeOnly(8, 0),
            new TimeOnly(22, 0),
            false,
            hasActiveDebt,
            false);

    private sealed class FakeAvailabilityRepository : IAvailabilityRepository
    {
        public ReservationMember? Member { get; init; } = new(1, MembershipCategory.Global, null, true);
        public ReservationContext? Context { get; init; }
        public int CreateCalls { get; private set; }
        public ReservationCommand? LastCommand { get; private set; }

        public Task<ReservationMember?> GetMemberAsync(string matricule, CancellationToken cancellationToken) =>
            Task.FromResult(Member);

        public Task<IReadOnlyList<AvailableSlot>> GetAvailabilityAsync(
            int siteId,
            DateOnly date,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AvailableSlot>>(Array.Empty<AvailableSlot>());

        public Task<ReservationContext?> GetReservationContextAsync(
            string matricule,
            int courtId,
            DateTime startAt,
            DateTime now,
            CancellationToken cancellationToken) =>
            Task.FromResult(Context);

        public Task<ReservationResult> CreateReservationAsync(
            ReservationCommand command,
            CancellationToken cancellationToken)
        {
            CreateCalls++;
            LastCommand = command;
            return Task.FromResult(new ReservationResult(
                42,
                command.CourtId,
                command.StartAt,
                command.StartAt.AddMinutes(90),
                command.Visibility));
        }
    }
}
