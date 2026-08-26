namespace PadelCourtManagement.Domain;

public sealed record MemberRequest(
    string Matricule,
    string DisplayName,
    char MembershipCategory,
    int? HomeSiteId);

public sealed record SiteRequest(string Name);

public sealed record CourtRequest(
    int SiteId,
    string Name,
    bool IsActive = true);

public sealed record ScheduleRequest(
    int SiteId,
    short CalendarYear,
    TimeOnly OpeningTime,
    TimeOnly ClosingTime);

public sealed record ClosureRequest(
    char Scope,
    int? SiteId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Reason);
