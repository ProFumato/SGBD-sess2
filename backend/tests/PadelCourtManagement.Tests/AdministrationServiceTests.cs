using PadelCourtManagement.Application.Administration;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Tests;

public sealed class AdministrationServiceTests
{
    [Fact]
    public async Task ChangingLastGlobalAdministratorToSiteRoleIsRejected()
    {
        var globalMember = new Member(1, "G0001", "Global administrator", MembershipCategory.Global, null, true);
        var repository = new FakeAdministrationRepository
        {
            ActiveAdministrator = new AdministratorActor(1, "G0001", AdministratorScope.Global, null),
            Member = globalMember,
            ActiveGlobalAdministratorCount = 1
        };
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<AdministrationConflictException>(() =>
            service.SetAdministratorRoleAsync(
                "G0001",
                "G0001",
                new AdministratorRoleInput(AdministratorScope.Site, 1),
                CancellationToken.None));

        Assert.Contains("At least one active global administrator", exception.Message);
    }

    [Fact]
    public async Task SiteAdministratorCannotCreateGlobalMember()
    {
        var repository = new FakeAdministrationRepository
        {
            ActiveAdministrator = new AdministratorActor(2, "S00001", AdministratorScope.Site, 1)
        };
        var service = CreateService(repository);

        await Assert.ThrowsAsync<AdministrationForbiddenException>(() =>
            service.CreateMemberAsync(
                "S00001",
                new MemberInput("G0002", "Global member", MembershipCategory.Global, null, true),
                CancellationToken.None));
    }

    [Fact]
    public async Task ScheduleExcludingExistingMatchIsRejected()
    {
        var repository = new FakeAdministrationRepository
        {
            ActiveAdministrator = new AdministratorActor(1, "G0001", AdministratorScope.Global, null),
            Site = new Site(1, "Brussels"),
            HasMatchOutsideSchedule = true
        };
        var service = CreateService(repository);

        await Assert.ThrowsAsync<AdministrationConflictException>(() =>
            service.SetScheduleAsync(
                "G0001",
                1,
                2030,
                new ScheduleInput(new TimeOnly(10, 30), new TimeOnly(22, 0)),
                CancellationToken.None));
    }

    [Fact]
    public async Task GlobalAdministratorCanSetScheduleWhenNoConflictExists()
    {
        var repository = new FakeAdministrationRepository
        {
            ActiveAdministrator = new AdministratorActor(1, "G0001", AdministratorScope.Global, null),
            Site = new Site(1, "Brussels"),
            Schedule = new SiteAnnualSchedule(1, 1, 2030, new TimeOnly(9, 0), new TimeOnly(22, 0))
        };
        var service = CreateService(repository);

        var result = await service.SetScheduleAsync(
            "G0001",
            1,
            2030,
            new ScheduleInput(new TimeOnly(9, 0), new TimeOnly(22, 0)),
            CancellationToken.None);

        Assert.Equal(1, result.SiteId);
        Assert.Equal(2030, result.CalendarYear);
    }

    [Fact]
    public async Task SiteAdministratorCanSetScheduleForAssignedSite()
    {
        var repository = new FakeAdministrationRepository
        {
            ActiveAdministrator = new AdministratorActor(2, "S00001", AdministratorScope.Site, 1),
            Site = new Site(1, "Brussels"),
            Schedule = new SiteAnnualSchedule(2, 1, 2030, new TimeOnly(8, 0), new TimeOnly(20, 0))
        };
        var service = CreateService(repository);

        var result = await service.SetScheduleAsync(
            "S00001",
            1,
            2030,
            new ScheduleInput(new TimeOnly(8, 0), new TimeOnly(20, 0)),
            CancellationToken.None);

        Assert.Equal(1, result.SiteId);
        Assert.Equal(2030, result.CalendarYear);
    }

    [Fact]
    public async Task SiteAdministratorCanCreateClosureForAssignedSite()
    {
        var repository = new FakeAdministrationRepository
        {
            ActiveAdministrator = new AdministratorActor(2, "S00001", AdministratorScope.Site, 1),
            Site = new Site(1, "Brussels"),
            Closure = new Closure(1, ClosureScope.Site, 1, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1), "Maintenance")
        };
        var service = CreateService(repository);

        var result = await service.CreateClosureAsync(
            "S00001",
            new ClosureInput(ClosureScope.Site, 1, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1), "Maintenance"),
            CancellationToken.None);

        Assert.Equal(ClosureScope.Site, result.Scope);
        Assert.Equal(1, result.SiteId);
    }

    [Fact]
    public async Task GlobalAdministratorCanCreateGlobalClosure()
    {
        var repository = new FakeAdministrationRepository
        {
            ActiveAdministrator = new AdministratorActor(1, "G0001", AdministratorScope.Global, null),
            Closure = new Closure(2, ClosureScope.Global, null, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(2), "Festival")
        };
        var service = CreateService(repository);

        var result = await service.CreateClosureAsync(
            "G0001",
            new ClosureInput(ClosureScope.Global, null, DateTime.UtcNow.AddDays(2), DateTime.UtcNow.AddDays(2).AddHours(2), "Festival"),
            CancellationToken.None);

        Assert.Equal(ClosureScope.Global, result.Scope);
        Assert.Null(result.SiteId);
    }

    [Fact]
    public async Task GlobalClosureConflictIsRejected()
    {
        var repository = new FakeAdministrationRepository
        {
            ActiveAdministrator = new AdministratorActor(1, "G0001", AdministratorScope.Global, null),
            HasMatchOverlappingClosure = true
        };
        var service = CreateService(repository);

        await Assert.ThrowsAsync<AdministrationConflictException>(() =>
            service.CreateClosureAsync(
                "G0001",
                new ClosureInput(ClosureScope.Global, null, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(1), "Test"),
                CancellationToken.None));
    }

    [Fact]
    public async Task RemovingScheduleWithExistingMatchesIsRejected()
    {
        var repository = new FakeAdministrationRepository
        {
            ActiveAdministrator = new AdministratorActor(1, "G0001", AdministratorScope.Global, null),
            Site = new Site(1, "Brussels"),
            HasMatchesInYear = true
        };
        var service = CreateService(repository);

        await Assert.ThrowsAsync<AdministrationConflictException>(() =>
            service.DeleteScheduleAsync("G0001", 1, 2030, CancellationToken.None));
    }

    private static AdministrationService CreateService(FakeAdministrationRepository repository) =>
        new(
            repository,
            repository,
            repository,
            repository,
            repository,
            repository,
            new AdministrationAuthorizer());

    private sealed class FakeAdministrationRepository : IMemberRepository, IAdministratorRepository, ISiteRepository, ICourtRepository, IScheduleRepository, IClosureRepository
    {
        public AdministratorActor? ActiveAdministrator { get; init; }
        public int ActiveGlobalAdministratorCount { get; init; }
        public Member? Member { get; init; }
        public Site? Site { get; init; }
        public bool HasMatchOutsideSchedule { get; init; }
        public bool HasMatchesInYear { get; init; }
        public bool HasMatchOverlappingClosure { get; init; }
        public SiteAnnualSchedule? Schedule { get; init; }
        public Closure? Closure { get; init; }

        public Task<AdministratorActor?> GetActiveAdministratorAsync(string matricule, CancellationToken cancellationToken) =>
            Task.FromResult(ActiveAdministrator);

        public Task<int> GetActiveGlobalAdministratorCountAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ActiveGlobalAdministratorCount);

        public Task<Member?> GetMemberByMatriculeAsync(string matricule, CancellationToken cancellationToken) =>
            Task.FromResult(Member);

        public Task<IReadOnlyList<Member>> GetMembersAsync(int? siteId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Member>>(Array.Empty<Member>());

        public Task<Member> CreateMemberAsync(MemberInput input, CancellationToken cancellationToken) =>
            Task.FromResult(Member!);

        public Task<Member> UpdateMemberAsync(int memberId, MemberInput input, CancellationToken cancellationToken) =>
            Task.FromResult(Member!);

        public Task<AdministratorActor?> GetActiveAdministratorAsync(string matricule, CancellationToken cancellationToken, bool dummy = false) =>
            Task.FromResult(ActiveAdministrator);

        public Task SetAdministratorRoleAsync(int memberId, AdministratorRoleInput input, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RemoveAdministratorRoleAsync(int memberId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<Site?> GetSiteAsync(int siteId, CancellationToken cancellationToken) =>
            Task.FromResult(Site);

        public Task<IReadOnlyList<Site>> GetSitesAsync(int? siteId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Site>>(Array.Empty<Site>());

        public Task<Site> CreateSiteAsync(SiteInput input, CancellationToken cancellationToken) =>
            Task.FromResult(Site!);

        public Task<Site> UpdateSiteAsync(int siteId, SiteInput input, CancellationToken cancellationToken) =>
            Task.FromResult(Site!);

        public Task<Court?> GetCourtAsync(int courtId, CancellationToken cancellationToken) =>
            Task.FromResult<Court?>(null);

        public Task<IReadOnlyList<Court>> GetCourtsAsync(int siteId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Court>>(Array.Empty<Court>());

        public Task<Court> CreateCourtAsync(int siteId, CourtInput input, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<Court> UpdateCourtAsync(int courtId, CourtInput input, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<bool> HasMatchOutsideScheduleAsync(int siteId, int calendarYear, TimeOnly openingTime, TimeOnly closingTime, CancellationToken cancellationToken) =>
            Task.FromResult(HasMatchOutsideSchedule);

        public Task<bool> HasMatchesInYearAsync(int siteId, int calendarYear, CancellationToken cancellationToken) =>
            Task.FromResult(HasMatchesInYear);

        public Task<IReadOnlyList<SiteAnnualSchedule>> GetSchedulesAsync(int siteId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SiteAnnualSchedule>>(Schedule is null ? Array.Empty<SiteAnnualSchedule>() : new[] { Schedule });

        public Task<SiteAnnualSchedule> SetScheduleAsync(int siteId, int calendarYear, ScheduleInput input, CancellationToken cancellationToken) =>
            Task.FromResult(Schedule ?? new SiteAnnualSchedule(1, siteId, calendarYear, input.OpeningTime, input.ClosingTime));

        public Task DeleteScheduleAsync(int siteId, int calendarYear, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<Closure?> GetClosureAsync(int closureId, CancellationToken cancellationToken) =>
            Task.FromResult<Closure?>(null);

        public Task<IReadOnlyList<Closure>> GetClosuresAsync(int? siteId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Closure>>(Array.Empty<Closure>());

        public Task<bool> HasMatchOverlappingClosureAsync(ClosureInput input, CancellationToken cancellationToken) =>
            Task.FromResult(HasMatchOverlappingClosure);

        public Task<Closure> CreateClosureAsync(ClosureInput input, CancellationToken cancellationToken) =>
            Task.FromResult(Closure ?? new Closure(1, input.Scope, input.SiteId, input.StartsAt, input.EndsAt, input.Reason));

        public Task<Closure> UpdateClosureAsync(int closureId, ClosureInput input, CancellationToken cancellationToken) =>
            Task.FromResult(Closure ?? new Closure(closureId, input.Scope, input.SiteId, input.StartsAt, input.EndsAt, input.Reason));

        public Task DeleteClosureAsync(int closureId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
