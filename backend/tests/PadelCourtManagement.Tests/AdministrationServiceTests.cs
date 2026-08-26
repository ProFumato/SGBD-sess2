using PadelCourtManagement.Application.Administration;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Tests;

public sealed class AdministrationServiceTests
{
    [Fact]
    public async Task ChangingLastGlobalAdministratorToSiteRoleIsRejected()
    {
        var members = new FakeMemberRepository
        {
            Member = new Member(1, "G0001", "Admin", MembershipCategory.Global, null, true)
        };
        var administrators = new FakeAdministratorRepository
        {
            ActiveAdministrator = new AdministratorActor(1, "G0001", AdministratorScope.Global, null),
            ActiveGlobalAdministratorCount = 1
        };
        var service = CreateService(members, administrators);

        await Assert.ThrowsAsync<AdministrationConflictException>(() =>
            service.SetAdministratorRoleAsync("G0001", "G0001", new AdministratorRoleInput(AdministratorScope.Site, 1), CancellationToken.None));
    }

    [Fact]
    public async Task SiteAdministratorCannotCreateGlobalMember()
    {
        var members = new FakeMemberRepository();
        var administrators = new FakeAdministratorRepository
        {
            ActiveAdministrator = new AdministratorActor(2, "S00001", AdministratorScope.Site, 1)
        };
        var service = CreateService(members, administrators);

        await Assert.ThrowsAsync<AdministrationForbiddenException>(() =>
            service.CreateMemberAsync("S00001", new MemberInput("G0002", "Global member", MembershipCategory.Global, null, true), CancellationToken.None));
    }

    [Fact]
    public async Task ScheduleExcludingExistingMatchIsRejected()
    {
        var members = new FakeMemberRepository();
        var sites = new FakeSiteRepository { Site = new Site(1, "Site A") };
        var schedules = new FakeScheduleRepository { HasMatchOutsideSchedule = true };
        var administrators = new FakeAdministratorRepository
        {
            ActiveAdministrator = new AdministratorActor(1, "G0001", AdministratorScope.Global, null)
        };
        var service = CreateService(members, administrators, sites: sites, schedules: schedules);

        await Assert.ThrowsAsync<AdministrationConflictException>(() =>
            service.SetScheduleAsync("G0001", 1, 2030, new ScheduleInput(new TimeOnly(10, 0), new TimeOnly(22, 0)), CancellationToken.None));
    }

    [Fact]
    public async Task GlobalClosureConflictIsRejected()
    {
        var members = new FakeMemberRepository();
        var closures = new FakeClosureRepository { HasMatchOverlappingClosure = true };
        var administrators = new FakeAdministratorRepository
        {
            ActiveAdministrator = new AdministratorActor(1, "G0001", AdministratorScope.Global, null)
        };
        var service = CreateService(members, administrators, closures: closures);

        await Assert.ThrowsAsync<AdministrationConflictException>(() =>
            service.CreateClosureAsync(
                "G0001",
                new ClosureInput(ClosureScope.Global, null, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1), "Test"),
                CancellationToken.None));
    }

    private static AdministrationService CreateService(
        FakeMemberRepository members,
        FakeAdministratorRepository administrators,
        FakeSiteRepository? sites = null,
        FakeCourtRepository? courts = null,
        FakeScheduleRepository? schedules = null,
        FakeClosureRepository? closures = null) =>
        new(
            members,
            administrators,
            sites ?? new FakeSiteRepository(),
            courts ?? new FakeCourtRepository(),
            schedules ?? new FakeScheduleRepository(),
            closures ?? new FakeClosureRepository(),
            new AdministrationAuthorizer());

    private sealed class FakeMemberRepository : IMemberRepository
    {
        public Member? Member { get; init; }
        public Task<Member?> GetMemberByMatriculeAsync(string matricule, CancellationToken cancellationToken) => Task.FromResult(Member);
        public Task<IReadOnlyList<Member>> GetMembersAsync(int? siteId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Member>>(Array.Empty<Member>());
        public Task<Member> CreateMemberAsync(MemberInput input, CancellationToken cancellationToken) => Task.FromResult(Member!);
        public Task<Member> UpdateMemberAsync(int memberId, MemberInput input, CancellationToken cancellationToken) => Task.FromResult(Member!);
    }

    private sealed class FakeAdministratorRepository : IAdministratorRepository
    {
        public AdministratorActor? ActiveAdministrator { get; init; }
        public int ActiveGlobalAdministratorCount { get; init; }
        public Task<AdministratorActor?> GetActiveAdministratorAsync(string matricule, CancellationToken cancellationToken) => Task.FromResult(ActiveAdministrator);
        public Task<int> GetActiveGlobalAdministratorCountAsync(CancellationToken cancellationToken) => Task.FromResult(ActiveGlobalAdministratorCount);
        public Task SetAdministratorRoleAsync(int memberId, AdministratorRoleInput input, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RemoveAdministratorRoleAsync(int memberId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeSiteRepository : ISiteRepository
    {
        public Site? Site { get; init; }
        public Task<Site?> GetSiteAsync(int siteId, CancellationToken cancellationToken) => Task.FromResult(Site);
        public Task<IReadOnlyList<Site>> GetSitesAsync(int? siteId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Site>>(Array.Empty<Site>());
        public Task<Site> CreateSiteAsync(SiteInput input, CancellationToken cancellationToken) => Task.FromResult(Site!);
        public Task<Site> UpdateSiteAsync(int siteId, SiteInput input, CancellationToken cancellationToken) => Task.FromResult(Site!);
    }

    private sealed class FakeCourtRepository : ICourtRepository
    {
        public Task<Court?> GetCourtAsync(int courtId, CancellationToken cancellationToken) => Task.FromResult<Court?>(null);
        public Task<IReadOnlyList<Court>> GetCourtsAsync(int siteId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Court>>(Array.Empty<Court>());
        public Task<Court> CreateCourtAsync(int siteId, CourtInput input, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Court> UpdateCourtAsync(int courtId, CourtInput input, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class FakeScheduleRepository : IScheduleRepository
    {
        public bool HasMatchOutsideSchedule { get; init; }
        public Task<bool> HasMatchOutsideScheduleAsync(int siteId, int calendarYear, TimeOnly openingTime, TimeOnly closingTime, CancellationToken cancellationToken) => Task.FromResult(HasMatchOutsideSchedule);
        public Task<IReadOnlyList<SiteAnnualSchedule>> GetSchedulesAsync(int siteId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SiteAnnualSchedule>>(Array.Empty<SiteAnnualSchedule>());
        public Task<SiteAnnualSchedule> SetScheduleAsync(int siteId, int calendarYear, ScheduleInput input, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task DeleteScheduleAsync(int siteId, int calendarYear, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeClosureRepository : IClosureRepository
    {
        public bool HasMatchOverlappingClosure { get; init; }
        public Task<Closure?> GetClosureAsync(int closureId, CancellationToken cancellationToken) => Task.FromResult<Closure?>(null);
        public Task<IReadOnlyList<Closure>> GetClosuresAsync(int? siteId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Closure>>(Array.Empty<Closure>());
        public Task<bool> HasMatchOverlappingClosureAsync(ClosureInput input, CancellationToken cancellationToken) => Task.FromResult(HasMatchOverlappingClosure);
        public Task<Closure> CreateClosureAsync(ClosureInput input, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Closure> UpdateClosureAsync(int closureId, ClosureInput input, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task DeleteClosureAsync(int closureId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
