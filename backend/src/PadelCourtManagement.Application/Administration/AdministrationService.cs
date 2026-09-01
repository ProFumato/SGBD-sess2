// Administration Service: Manage club operations and member data.
// Create/edit members, sites, courts, schedules, closures. Authenticate users and authorize actions.

using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application.Administration;

public sealed class AdministrationService(
    IMemberRepository members,
    IAdministratorRepository administrators,
    ISiteRepository sites,
    ICourtRepository courts,
    IScheduleRepository schedules,
    IClosureRepository closures,
    AdministrationAuthorizer authorizer) : IAdministrationService
{
    public async Task<IdentityResult> IdentifyAsync(string matricule, CancellationToken cancellationToken)
    {
        var normalizedMatricule = NormalizeMatricule(matricule);
        var member = await members.GetMemberByMatriculeAsync(normalizedMatricule, cancellationToken)
            ?? throw new AdministrationNotFoundException("The matricule does not identify a member.");

        var role = member.IsActive
            ? await administrators.GetActiveAdministratorAsync(normalizedMatricule, cancellationToken)
            : null;

        return new IdentityResult(member, role);
    }

    public async Task<IReadOnlyList<Member>> GetMembersAsync(string actorMatricule, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        return await members.GetMembersAsync(actor.Scope == AdministratorScope.Site ? actor.SiteId : null, cancellationToken);
    }

    public async Task<Member> CreateMemberAsync(string actorMatricule, MemberInput input, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        var normalizedInput = ValidateMemberInput(input);
        await ValidateMemberHomeSiteAsync(normalizedInput, cancellationToken);
        await AuthorizeMemberInputAsync(actor, normalizedInput, cancellationToken);
        return await members.CreateMemberAsync(normalizedInput, cancellationToken);
    }

    public async Task<Member> UpdateMemberAsync(string actorMatricule, string memberMatricule, MemberInput input, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        var member = await GetMemberAsync(memberMatricule, cancellationToken);
        authorizer.RequireMemberAccess(actor, member);

        var normalizedInput = ValidateMemberInput(input);
        if (!normalizedInput.IsActive && await IsActiveGlobalAdministratorAsync(member, cancellationToken))
        {
            await EnsureActiveGlobalAdministratorRemainsAsync(cancellationToken);
        }

        await ValidateMemberHomeSiteAsync(normalizedInput, cancellationToken);
        await AuthorizeMemberInputAsync(actor, normalizedInput, cancellationToken);
        return await members.UpdateMemberAsync(member.MemberId, normalizedInput, cancellationToken);
    }

    public async Task SetMemberActivationAsync(string actorMatricule, string memberMatricule, bool isActive, CancellationToken cancellationToken)
    {
        var member = await GetMemberAsync(memberMatricule, cancellationToken);
        await UpdateMemberAsync(
            actorMatricule,
            memberMatricule,
            new MemberInput(member.Matricule, member.DisplayName, member.MembershipCategory, member.HomeSiteId, isActive),
            cancellationToken);
    }

    public async Task SetAdministratorRoleAsync(string actorMatricule, string memberMatricule, AdministratorRoleInput input, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        authorizer.RequireGlobal(actor);

        var member = await GetMemberAsync(memberMatricule, cancellationToken);
        if (!member.IsActive)
        {
            throw new AdministrationValidationException("An inactive member cannot receive an administrator role.");
        }

        if (await IsActiveGlobalAdministratorAsync(member, cancellationToken)
            && input.Scope != AdministratorScope.Global)
        {
            await EnsureActiveGlobalAdministratorRemainsAsync(cancellationToken);
        }

        await ValidateRoleInputAsync(input, cancellationToken);
        await administrators.SetAdministratorRoleAsync(member.MemberId, input, cancellationToken);
    }

    public async Task RemoveAdministratorRoleAsync(string actorMatricule, string memberMatricule, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        authorizer.RequireGlobal(actor);
        var member = await GetMemberAsync(memberMatricule, cancellationToken);
        if (await IsActiveGlobalAdministratorAsync(member, cancellationToken))
        {
            await EnsureActiveGlobalAdministratorRemainsAsync(cancellationToken);
        }

        await administrators.RemoveAdministratorRoleAsync(member.MemberId, cancellationToken);
    }

    public async Task<IReadOnlyList<Site>> GetSitesAsync(string actorMatricule, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        return await sites.GetSitesAsync(actor.Scope == AdministratorScope.Site ? actor.SiteId : null, cancellationToken);
    }

    public async Task<Site> CreateSiteAsync(string actorMatricule, SiteInput input, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        authorizer.RequireGlobal(actor);
        return await sites.CreateSiteAsync(ValidateSiteInput(input), cancellationToken);
    }

    public async Task<Site> UpdateSiteAsync(string actorMatricule, int siteId, SiteInput input, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        await RequireSiteAsync(actor, siteId, cancellationToken);
        return await sites.UpdateSiteAsync(siteId, ValidateSiteInput(input), cancellationToken);
    }

    public async Task<IReadOnlyList<Court>> GetCourtsAsync(string actorMatricule, int siteId, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        await RequireSiteAsync(actor, siteId, cancellationToken);
        return await courts.GetCourtsAsync(siteId, cancellationToken);
    }

    public async Task<Court> CreateCourtAsync(string actorMatricule, int siteId, CourtInput input, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        await RequireSiteAsync(actor, siteId, cancellationToken);
        return await courts.CreateCourtAsync(siteId, ValidateCourtInput(input), cancellationToken);
    }

    public async Task<Court> UpdateCourtAsync(string actorMatricule, int courtId, CourtInput input, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        var court = await courts.GetCourtAsync(courtId, cancellationToken)
            ?? throw new AdministrationNotFoundException("The court does not exist.");
        authorizer.RequireSiteAccess(actor, court.SiteId);
        return await courts.UpdateCourtAsync(courtId, ValidateCourtInput(input), cancellationToken);
    }

    public async Task<IReadOnlyList<SiteAnnualSchedule>> GetSchedulesAsync(string actorMatricule, int siteId, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        await RequireSiteAsync(actor, siteId, cancellationToken);
        return await schedules.GetSchedulesAsync(siteId, cancellationToken);
    }

    public async Task<SiteAnnualSchedule> SetScheduleAsync(string actorMatricule, int siteId, int calendarYear, ScheduleInput input, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        await RequireSiteAsync(actor, siteId, cancellationToken);
        ValidateScheduleInput(calendarYear, input);

        var openingTime = input.GetOpeningTime();
        var closingTime = input.GetClosingTime();

        if (await schedules.HasMatchOutsideScheduleAsync(siteId, calendarYear, openingTime, closingTime, cancellationToken))
        {
            throw new AdministrationConflictException("The schedule would make an existing match fall outside the site's opening hours.");
        }

        return await schedules.SetScheduleAsync(siteId, calendarYear, input, cancellationToken);
    }

    public async Task DeleteScheduleAsync(string actorMatricule, int siteId, int calendarYear, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        await RequireSiteAsync(actor, siteId, cancellationToken);
        ValidateCalendarYear(calendarYear);
        if (await schedules.HasMatchesInYearAsync(siteId, calendarYear, cancellationToken))
        {
            throw new AdministrationConflictException(
                "The schedule cannot be removed while the site has matches in that calendar year.");
        }

        await schedules.DeleteScheduleAsync(siteId, calendarYear, cancellationToken);
    }

    public async Task<IReadOnlyList<Closure>> GetClosuresAsync(string actorMatricule, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        return await closures.GetClosuresAsync(actor.Scope == AdministratorScope.Site ? actor.SiteId : null, cancellationToken);
    }

    public async Task<Closure> CreateClosureAsync(string actorMatricule, ClosureInput input, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        var normalizedInput = ValidateClosureInput(input);
        await AuthorizeClosureAsync(actor, normalizedInput, cancellationToken);
        await EnsureClosureDoesNotConflictAsync(normalizedInput, cancellationToken);
        return await closures.CreateClosureAsync(normalizedInput, cancellationToken);
    }

    public async Task<Closure> UpdateClosureAsync(string actorMatricule, int closureId, ClosureInput input, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        var existingClosure = await closures.GetClosureAsync(closureId, cancellationToken)
            ?? throw new AdministrationNotFoundException("The closure does not exist.");
        await AuthorizeClosureAsync(actor, ToInput(existingClosure), cancellationToken);

        var normalizedInput = ValidateClosureInput(input);
        await AuthorizeClosureAsync(actor, normalizedInput, cancellationToken);
        await EnsureClosureDoesNotConflictAsync(normalizedInput, cancellationToken);
        return await closures.UpdateClosureAsync(closureId, normalizedInput, cancellationToken);
    }

    public async Task DeleteClosureAsync(string actorMatricule, int closureId, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        var closure = await closures.GetClosureAsync(closureId, cancellationToken)
            ?? throw new AdministrationNotFoundException("The closure does not exist.");
        await AuthorizeClosureAsync(actor, ToInput(closure), cancellationToken);
        await closures.DeleteClosureAsync(closureId, cancellationToken);
    }

    private async Task<AdministratorActor> GetActorAsync(string actorMatricule, CancellationToken cancellationToken)
    {
        var normalizedMatricule = NormalizeMatricule(actorMatricule);
        return await administrators.GetActiveAdministratorAsync(normalizedMatricule, cancellationToken)
            ?? throw new AdministrationForbiddenException("The acting matricule is not an active administrator.");
    }

    private async Task<Member> GetMemberAsync(string matricule, CancellationToken cancellationToken)
    {
        var normalizedMatricule = NormalizeMatricule(matricule);
        return await members.GetMemberByMatriculeAsync(normalizedMatricule, cancellationToken)
            ?? throw new AdministrationNotFoundException("The member does not exist.");
    }

    private async Task RequireSiteAsync(AdministratorActor actor, int siteId, CancellationToken cancellationToken)
    {
        _ = await sites.GetSiteAsync(siteId, cancellationToken)
            ?? throw new AdministrationNotFoundException("The site does not exist.");
        authorizer.RequireSiteAccess(actor, siteId);
    }

    private async Task AuthorizeMemberInputAsync(AdministratorActor actor, MemberInput input, CancellationToken cancellationToken)
    {
        if (actor.Scope == AdministratorScope.Global)
        {
            return;
        }

        if (input.MembershipCategory != MembershipCategory.Site || input.HomeSiteId != actor.SiteId)
        {
            throw new AdministrationForbiddenException("A site administrator can create or update only members of the assigned site.");
        }

        _ = await sites.GetSiteAsync(input.HomeSiteId!.Value, cancellationToken)
            ?? throw new AdministrationNotFoundException("The member home site does not exist.");
    }

    private async Task ValidateMemberHomeSiteAsync(MemberInput input, CancellationToken cancellationToken)
    {
        if (input.MembershipCategory != MembershipCategory.Site)
        {
            if (input.HomeSiteId is not null)
            {
                throw new AdministrationValidationException("Only site members can have a home site.");
            }

            return;
        }

        if (input.HomeSiteId is null)
        {
            throw new AdministrationValidationException("Site members require a home site.");
        }

        _ = await sites.GetSiteAsync(input.HomeSiteId.Value, cancellationToken)
            ?? throw new AdministrationNotFoundException("The member home site does not exist.");
    }

    private static MemberInput ValidateMemberInput(MemberInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Matricule))
        {
            throw new AdministrationValidationException("The matricule is required.");
        }

        if (string.IsNullOrWhiteSpace(input.DisplayName))
        {
            throw new AdministrationValidationException("The display name is required.");
        }

        var normalizedInput = input with
        {
            Matricule = NormalizeMatricule(input.Matricule),
            DisplayName = input.DisplayName.Trim()
        };

        if (normalizedInput.DisplayName.Length > 120)
        {
            throw new AdministrationValidationException("The display name cannot exceed 120 characters.");
        }

        if (!HasValidMatriculeFormat(normalizedInput.Matricule, normalizedInput.MembershipCategory))
        {
            throw new AdministrationValidationException(
                "The matricule must match the selected membership category.");
        }

        return normalizedInput;
    }

    private static SiteInput ValidateSiteInput(SiteInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new AdministrationValidationException("The site name is required.");
        }

        var name = input.Name.Trim();
        if (name.Length > 100)
        {
            throw new AdministrationValidationException("The site name cannot exceed 100 characters.");
        }

        return input with { Name = name };
    }

    private static CourtInput ValidateCourtInput(CourtInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new AdministrationValidationException("The court name is required.");
        }

        var name = input.Name.Trim();
        if (name.Length > 100)
        {
            throw new AdministrationValidationException("The court name cannot exceed 100 characters.");
        }

        return input with { Name = name };
    }

    private static void ValidateScheduleInput(int calendarYear, ScheduleInput input)
    {
        ValidateCalendarYear(calendarYear);

        var openingTime = input.GetOpeningTime();
        var closingTime = input.GetClosingTime();
        if (openingTime >= closingTime)
        {
            throw new AdministrationValidationException("The opening time must be before the closing time.");
        }
    }

    private static ClosureInput ValidateClosureInput(ClosureInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new AdministrationValidationException("The closure reason is required.");
        }

        if (input.StartsAt >= input.EndsAt)
        {
            throw new AdministrationValidationException("The closure start time must be before the end time.");
        }

        if (input.Scope == ClosureScope.Site && input.SiteId is null)
        {
            throw new AdministrationValidationException("Site closures require a site.");
        }

        if (input.Scope == ClosureScope.Global && input.SiteId is not null)
        {
            throw new AdministrationValidationException("Global closures cannot target a site.");
        }

        var reason = input.Reason.Trim();
        if (reason.Length > 250)
        {
            throw new AdministrationValidationException("The closure reason cannot exceed 250 characters.");
        }

        return input with { Reason = reason };
    }

    private async Task EnsureActiveGlobalAdministratorRemainsAsync(CancellationToken cancellationToken)
    {
        if (await administrators.GetActiveGlobalAdministratorCountAsync(cancellationToken) <= 1)
        {
            throw new AdministrationConflictException("At least one active global administrator must remain.");
        }
    }

    private async Task<bool> IsActiveGlobalAdministratorAsync(Member member, CancellationToken cancellationToken)
    {
        var role = await administrators.GetActiveAdministratorAsync(member.Matricule, cancellationToken);
        return role is { Scope: AdministratorScope.Global };
    }

    private async Task ValidateRoleInputAsync(AdministratorRoleInput input, CancellationToken cancellationToken)
    {
        if (input.Scope == AdministratorScope.Site && input.SiteId is null)
        {
            throw new AdministrationValidationException("Site administrators require a site.");
        }

        if (input.Scope == AdministratorScope.Global && input.SiteId is not null)
        {
            throw new AdministrationValidationException("Global administrators cannot be assigned to a site.");
        }

        if (input.Scope == AdministratorScope.Site)
        {
            _ = await sites.GetSiteAsync(input.SiteId!.Value, cancellationToken)
                ?? throw new AdministrationNotFoundException("The administrator site does not exist.");
        }
    }

    private async Task AuthorizeClosureAsync(AdministratorActor actor, ClosureInput input, CancellationToken cancellationToken)
    {
        if (actor.Scope == AdministratorScope.Global)
        {
            return;
        }

        if (input.Scope != ClosureScope.Site || input.SiteId != actor.SiteId)
        {
            throw new AdministrationForbiddenException("A site administrator can manage only closures of the assigned site.");
        }

        _ = await sites.GetSiteAsync(input.SiteId!.Value, cancellationToken)
            ?? throw new AdministrationNotFoundException("The closure site does not exist.");
    }

    private async Task EnsureClosureDoesNotConflictAsync(ClosureInput input, CancellationToken cancellationToken)
    {
        if (await closures.HasMatchOverlappingClosureAsync(input, cancellationToken))
        {
            throw new AdministrationConflictException("The closure conflicts with an existing match.");
        }
    }

    private static ClosureInput ToInput(Closure closure) => new(closure.Scope, closure.SiteId, closure.StartsAt, closure.EndsAt, closure.Reason);

    private static string NormalizeMatricule(string matricule) => matricule.Trim().ToUpperInvariant();

    private static void ValidateCalendarYear(int calendarYear)
    {
        if (calendarYear is < 2000 or > 9999)
        {
            throw new AdministrationValidationException("The calendar year is not valid.");
        }
    }

    private static bool HasValidMatriculeFormat(string matricule, MembershipCategory category)
    {
        var expectedPrefix = category switch
        {
            MembershipCategory.Global => 'G',
            MembershipCategory.Site => 'S',
            MembershipCategory.Free => 'L',
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };
        var expectedLength = category == MembershipCategory.Global ? 5 : 6;

        return matricule.Length == expectedLength
            && matricule[0] == expectedPrefix
            && matricule[1..].All(character => character is >= '0' and <= '9');
    }
}
