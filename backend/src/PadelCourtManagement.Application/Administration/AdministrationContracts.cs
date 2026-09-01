// Administration Contracts: API request/response data types.
// Input models for members, admins, sites, courts, schedules, and closures.

using System.Globalization;
using System.Text.Json.Serialization;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application.Administration;

public sealed record MemberInput(
    [property: JsonPropertyName("matricule")] string Matricule,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("membershipCategory")] MembershipCategory MembershipCategory,
    [property: JsonPropertyName("homeSiteId")] int? HomeSiteId,
    [property: JsonPropertyName("isActive")] bool IsActive);

public sealed record AdministratorRoleInput(
    [property: JsonPropertyName("scope")] AdministratorScope Scope,
    [property: JsonPropertyName("siteId")] int? SiteId);

public sealed record SiteInput(
    [property: JsonPropertyName("name")] string Name);

public sealed record CourtInput(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("isActive")] bool IsActive);

public sealed class ScheduleInput
{
    [JsonPropertyName("openingTime")]
    public string OpeningTime { get; init; } = string.Empty;

    [JsonPropertyName("closingTime")]
    public string ClosingTime { get; init; } = string.Empty;

    [JsonConstructor]
    public ScheduleInput(string openingTime, string closingTime)
    {
        OpeningTime = openingTime ?? string.Empty;
        ClosingTime = closingTime ?? string.Empty;
    }

    public ScheduleInput(TimeOnly openingTime, TimeOnly closingTime)
        : this(openingTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture), closingTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture))
    {
    }

    public TimeOnly GetOpeningTime() => ParseTime(OpeningTime, nameof(OpeningTime));

    public TimeOnly GetClosingTime() => ParseTime(ClosingTime, nameof(ClosingTime));

    private static TimeOnly ParseTime(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"The {parameterName} value is not a valid time.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.EndsWith("Z", StringComparison.Ordinal))
        {
            normalized = normalized[..^1];
        }

        var parts = normalized.Split(':');
        if (parts.Length >= 4)
        {
            normalized = $"{parts[0]}:{parts[1]}:{parts[2]}";
        }

        if (TimeOnly.TryParse(normalized, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (TimeOnly.TryParseExact(normalized, new[]
            {
                "HH:mm",
                "HH:mm:ss",
                "HH:mm:ss.FFFFFFF",
                "HH:mm:ss.fffffff",
                "HH:mm:ss.ffffff",
                "HH:mm:ss.fffff",
                "HH:mm:ss.ffff",
                "HH:mm:ss.fff",
                "HH:mm:ss.ff",
                "HH:mm:ss.f"
            }, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"The {parameterName} value is not a valid time.", parameterName);
    }
}

public sealed record ClosureInput(
    [property: JsonPropertyName("scope")] ClosureScope Scope,
    [property: JsonPropertyName("siteId")] int? SiteId,
    [property: JsonPropertyName("startsAt")] DateTime StartsAt,
    [property: JsonPropertyName("endsAt")] DateTime EndsAt,
    [property: JsonPropertyName("reason")] string Reason);

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
