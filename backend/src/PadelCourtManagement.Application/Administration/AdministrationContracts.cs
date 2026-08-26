using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application.Administration;

public sealed record MemberInput(
    string Matricule,
    string DisplayName,
    MembershipCategory MembershipCategory,
    int? HomeSiteId,
    bool IsActive);

public sealed record AdministratorRoleInput(
    AdministratorScope Scope,
    int? SiteId);

public sealed record SiteInput(string Name);

public sealed record CourtInput(string Name, bool IsActive);

public sealed record ScheduleInput(TimeOnly OpeningTime, TimeOnly ClosingTime);

public sealed record ClosureInput(
    ClosureScope Scope,
    int? SiteId,
    DateTime StartsAt,
    DateTime EndsAt,
    string Reason);

public sealed record IdentityResult(Member Member, AdministratorActor? AdministratorRole);

public interface IMemberRepository
{
    Task<Member?> GetMemberByMatriculeAsync(string matricule, CancellationToken cancellationToken);
    Task<IReadOnlyList<Member>> GetMembersAsync(int? siteId, CancellationToken cancellationToken);
    Task<Member> CreateMemberAsync(MemberInput input, CancellationToken cancellationToken);
    Task<Member> UpdateMemberAsync(int memberId, MemberInput input, CancellationToken cancellationToken);
}

public interface IAdministratorRepository
{
    Task<AdministratorActor?> GetActiveAdministratorAsync(string matricule, CancellationToken cancellationToken);
    Task<int> GetActiveGlobalAdministratorCountAsync(CancellationToken cancellationToken);
    Task SetAdministratorRoleAsync(int memberId, AdministratorRoleInput input, CancellationToken cancellationToken);
    Task RemoveAdministratorRoleAsync(int memberId, CancellationToken cancellationToken);
}

public interface ISiteRepository
{
    Task<Site?> GetSiteAsync(int siteId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Site>> GetSitesAsync(int? siteId, CancellationToken cancellationToken);
    Task<Site> CreateSiteAsync(SiteInput input, CancellationToken cancellationToken);
    Task<Site> UpdateSiteAsync(int siteId, SiteInput input, CancellationToken cancellationToken);
}

public interface ICourtRepository
{
    Task<Court?> GetCourtAsync(int courtId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Court>> GetCourtsAsync(int siteId, CancellationToken cancellationToken);
    Task<Court> CreateCourtAsync(int siteId, CourtInput input, CancellationToken cancellationToken);
    Task<Court> UpdateCourtAsync(int courtId, CourtInput input, CancellationToken cancellationToken);
}

public interface IScheduleRepository
{
    Task<bool> HasMatchesInYearAsync(int siteId, int calendarYear, CancellationToken cancellationToken);
    Task<bool> HasMatchOutsideScheduleAsync(int siteId, int calendarYear, TimeOnly openingTime, TimeOnly closingTime, CancellationToken cancellationToken);
    Task<IReadOnlyList<SiteAnnualSchedule>> GetSchedulesAsync(int siteId, CancellationToken cancellationToken);
    Task<SiteAnnualSchedule> SetScheduleAsync(int siteId, int calendarYear, ScheduleInput input, CancellationToken cancellationToken);
    Task DeleteScheduleAsync(int siteId, int calendarYear, CancellationToken cancellationToken);
}

public interface IClosureRepository
{
    Task<Closure?> GetClosureAsync(int closureId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Closure>> GetClosuresAsync(int? siteId, CancellationToken cancellationToken);
    Task<bool> HasMatchOverlappingClosureAsync(ClosureInput input, CancellationToken cancellationToken);
    Task<Closure> CreateClosureAsync(ClosureInput input, CancellationToken cancellationToken);
    Task<Closure> UpdateClosureAsync(int closureId, ClosureInput input, CancellationToken cancellationToken);
    Task DeleteClosureAsync(int closureId, CancellationToken cancellationToken);
}

public interface IAdministrationService
{
    Task<IdentityResult> IdentifyAsync(string matricule, CancellationToken cancellationToken);
    Task<IReadOnlyList<Member>> GetMembersAsync(string actorMatricule, CancellationToken cancellationToken);
    Task<Member> CreateMemberAsync(string actorMatricule, MemberInput input, CancellationToken cancellationToken);
    Task<Member> UpdateMemberAsync(string actorMatricule, string memberMatricule, MemberInput input, CancellationToken cancellationToken);
    Task SetMemberActivationAsync(string actorMatricule, string memberMatricule, bool isActive, CancellationToken cancellationToken);
    Task SetAdministratorRoleAsync(string actorMatricule, string memberMatricule, AdministratorRoleInput input, CancellationToken cancellationToken);
    Task RemoveAdministratorRoleAsync(string actorMatricule, string memberMatricule, CancellationToken cancellationToken);
    Task<IReadOnlyList<Site>> GetSitesAsync(string actorMatricule, CancellationToken cancellationToken);
    Task<Site> CreateSiteAsync(string actorMatricule, SiteInput input, CancellationToken cancellationToken);
    Task<Site> UpdateSiteAsync(string actorMatricule, int siteId, SiteInput input, CancellationToken cancellationToken);
    Task<IReadOnlyList<Court>> GetCourtsAsync(string actorMatricule, int siteId, CancellationToken cancellationToken);
    Task<Court> CreateCourtAsync(string actorMatricule, int siteId, CourtInput input, CancellationToken cancellationToken);
    Task<Court> UpdateCourtAsync(string actorMatricule, int courtId, CourtInput input, CancellationToken cancellationToken);
    Task<IReadOnlyList<SiteAnnualSchedule>> GetSchedulesAsync(string actorMatricule, int siteId, CancellationToken cancellationToken);
    Task<SiteAnnualSchedule> SetScheduleAsync(string actorMatricule, int siteId, int calendarYear, ScheduleInput input, CancellationToken cancellationToken);
    Task DeleteScheduleAsync(string actorMatricule, int siteId, int calendarYear, CancellationToken cancellationToken);
    Task<IReadOnlyList<Closure>> GetClosuresAsync(string actorMatricule, CancellationToken cancellationToken);
    Task<Closure> CreateClosureAsync(string actorMatricule, ClosureInput input, CancellationToken cancellationToken);
    Task<Closure> UpdateClosureAsync(string actorMatricule, int closureId, ClosureInput input, CancellationToken cancellationToken);
    Task DeleteClosureAsync(string actorMatricule, int closureId, CancellationToken cancellationToken);
}
