using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

public sealed class AdminApiService
{
    private readonly IAdminApiRepository repository;

    public AdminApiService(IAdminApiRepository repository)
    {
        this.repository = repository;
    }

    public IReadOnlyList<MemberRecord> GetMembers() => repository.GetMembers();
    public int CreateMember(MemberRequest request) => repository.CreateMember(request);
    public int UpdateMember(int memberId, MemberRequest request) => repository.UpdateMember(memberId, request);
    public void DeleteMember(int memberId) => repository.DeleteMember(memberId);

    public IReadOnlyList<SiteRecord> GetSites() => repository.GetSites();
    public int CreateSite(SiteRequest request) => repository.CreateSite(request);
    public int UpdateSite(int siteId, SiteRequest request) => repository.UpdateSite(siteId, request);
    public void DeleteSite(int siteId) => repository.DeleteSite(siteId);

    public IReadOnlyList<CourtRecord> GetCourts() => repository.GetCourts();
    public int CreateCourt(CourtRequest request) => repository.CreateCourt(request);
    public int UpdateCourt(int courtId, CourtRequest request) => repository.UpdateCourt(courtId, request);
    public void DeleteCourt(int courtId) => repository.DeleteCourt(courtId);

    public IReadOnlyList<ScheduleRecord> GetSchedules() => repository.GetSchedules();
    public int CreateSchedule(ScheduleRequest request) => repository.CreateSchedule(request);
    public int UpdateSchedule(int scheduleId, ScheduleRequest request) => repository.UpdateSchedule(scheduleId, request);
    public void DeleteSchedule(int scheduleId) => repository.DeleteSchedule(scheduleId);

    public IReadOnlyList<ClosureRecord> GetClosures() => repository.GetClosures();
    public int CreateClosure(ClosureRequest request) => repository.CreateClosure(request);
    public int UpdateClosure(int closureId, ClosureRequest request) => repository.UpdateClosure(closureId, request);
    public void DeleteClosure(int closureId) => repository.DeleteClosure(closureId);
}
