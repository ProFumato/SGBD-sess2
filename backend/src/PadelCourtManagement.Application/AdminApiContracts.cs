using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

public interface IAdminApiRepository
{
    IReadOnlyList<MemberRecord> GetMembers();
    int CreateMember(MemberRequest request);
    int UpdateMember(int memberId, MemberRequest request);
    void DeleteMember(int memberId);

    IReadOnlyList<SiteRecord> GetSites();
    int CreateSite(SiteRequest request);
    int UpdateSite(int siteId, SiteRequest request);
    void DeleteSite(int siteId);

    IReadOnlyList<CourtRecord> GetCourts();
    int CreateCourt(CourtRequest request);
    int UpdateCourt(int courtId, CourtRequest request);
    void DeleteCourt(int courtId);

    IReadOnlyList<ScheduleRecord> GetSchedules();
    int CreateSchedule(ScheduleRequest request);
    int UpdateSchedule(int scheduleId, ScheduleRequest request);
    void DeleteSchedule(int scheduleId);

    IReadOnlyList<ClosureRecord> GetClosures();
    int CreateClosure(ClosureRequest request);
    int UpdateClosure(int closureId, ClosureRequest request);
    void DeleteClosure(int closureId);
}

public sealed record MemberRecord(int MemberId, string Matricule, string DisplayName, char MembershipCategory, int? HomeSiteId, bool IsActive);
public sealed record SiteRecord(int SiteId, string Name);
public sealed record CourtRecord(int CourtId, int SiteId, string Name, bool IsActive);
public sealed record ScheduleRecord(int SiteAnnualScheduleId, int SiteId, short CalendarYear, TimeOnly OpeningTime, TimeOnly ClosingTime);
public sealed record ClosureRecord(int ClosureId, char Scope, int? SiteId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Reason);
