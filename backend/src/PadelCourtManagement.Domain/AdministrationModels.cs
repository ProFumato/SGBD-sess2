namespace PadelCourtManagement.Domain;

public enum MembershipCategory
{
    Global,
    Site,
    Free
}

public enum AdministratorScope
{
    Global,
    Site
}

public sealed record Member(
    int MemberId,
    string Matricule,
    string DisplayName,
    MembershipCategory MembershipCategory,
    int? HomeSiteId,
    bool IsActive);

public sealed record AdministratorActor(
    int MemberId,
    string Matricule,
    AdministratorScope Scope,
    int? SiteId);

public sealed record Site(int SiteId, string Name);

public sealed record Court(int CourtId, int SiteId, string Name, bool IsActive);

public sealed record SiteAnnualSchedule(
    int SiteAnnualScheduleId,
    int SiteId,
    int CalendarYear,
    TimeOnly OpeningTime,
    TimeOnly ClosingTime);

public sealed record Closure(
    int ClosureId,
    AdministratorScope Scope,
    int? SiteId,
    DateTime StartsAt,
    DateTime EndsAt,
    string Reason);
