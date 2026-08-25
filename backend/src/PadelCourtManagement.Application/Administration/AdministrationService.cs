using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application.Administration;

public sealed class AdministrationService(
    IAdministrationRepository repository,
    AdministrationAuthorizer authorizer) : IAdministrationService
{
    public async Task<IdentityResult> IdentifyAsync(string matricule, CancellationToken cancellationToken)
    {
        var normalizedMatricule = NormalizeMatricule(matricule);
        var member = await repository.GetMemberByMatriculeAsync(normalizedMatricule, cancellationToken)
            ?? throw new AdministrationNotFoundException("The matricule does not identify a member.");

        var role = member.IsActive
            ? await repository.GetActiveAdministratorAsync(normalizedMatricule, cancellationToken)
            : null;

        return new IdentityResult(member, role);
    }

    public async Task<IReadOnlyList<Member>> GetMembersAsync(string actorMatricule, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        return await repository.GetMembersAsync(
            actor.Scope == AdministratorScope.Site ? actor.SiteId : null,
            cancellationToken);
    }

    public async Task<Member> CreateMemberAsync(string actorMatricule, MemberInput input, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        var normalizedInput = ValidateMemberInput(input);
        await ValidateMemberHomeSiteAsync(normalizedInput, cancellationToken);
        await AuthorizeMemberInputAsync(actor, normalizedInput, cancellationToken);
        return await repository.CreateMemberAsync(normalizedInput, cancellationToken);
    }

    public async Task<Member> UpdateMemberAsync(
        string actorMatricule,
        string memberMatricule,
        MemberInput input,
        CancellationToken cancellationToken)
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
        return await repository.UpdateMemberAsync(member.MemberId, normalizedInput, cancellationToken);
    }

    public async Task SetMemberActivationAsync(
        string actorMatricule,
        string memberMatricule,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var member = await GetMemberAsync(memberMatricule, cancellationToken);
        await UpdateMemberAsync(
            actorMatricule,
            memberMatricule,
            new MemberInput(
                member.Matricule,
                member.DisplayName,
                member.MembershipCategory,
                member.HomeSiteId,
                isActive),
            cancellationToken);
    }

    public async Task SetAdministratorRoleAsync(
        string actorMatricule,
        string memberMatricule,
        AdministratorRoleInput input,
        CancellationToken cancellationToken)
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
        await repository.SetAdministratorRoleAsync(member.MemberId, input, cancellationToken);
    }

    public async Task RemoveAdministratorRoleAsync(
        string actorMatricule,
        string memberMatricule,
        CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        authorizer.RequireGlobal(actor);
        var member = await GetMemberAsync(memberMatricule, cancellationToken);
        if (await IsActiveGlobalAdministratorAsync(member, cancellationToken))
        {
            await EnsureActiveGlobalAdministratorRemainsAsync(cancellationToken);
        }

        await repository.RemoveAdministratorRoleAsync(member.MemberId, cancellationToken);
    }

    public async Task<IReadOnlyList<Site>> GetSitesAsync(string actorMatricule, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        return await repository.GetSitesAsync(
            actor.Scope == AdministratorScope.Site ? actor.SiteId : null,
            cancellationToken);
    }

    public async Task<Site> CreateSiteAsync(string actorMatricule, SiteInput input, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        authorizer.RequireGlobal(actor);
        return await repository.CreateSiteAsync(ValidateSiteInput(input), cancellationToken);
    }

    public async Task<Site> UpdateSiteAsync(string actorMatricule, int siteId, SiteInput input, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        await RequireSiteAsync(actor, siteId, cancellationToken);
        return await repository.UpdateSiteAsync(siteId, ValidateSiteInput(input), cancellationToken);
    }

    public async Task<IReadOnlyList<Court>> GetCourtsAsync(string actorMatricule, int siteId, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        await RequireSiteAsync(actor, siteId, cancellationToken);
        return await repository.GetCourtsAsync(siteId, cancellationToken);
    }

    public async Task<Court> CreateCourtAsync(string actorMatricule, int siteId, CourtInput input, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        await RequireSiteAsync(actor, siteId, cancellationToken);
        return await repository.CreateCourtAsync(siteId, ValidateCourtInput(input), cancellationToken);
    }

    public async Task<Court> UpdateCourtAsync(string actorMatricule, int courtId, CourtInput input, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        var court = await repository.GetCourtAsync(courtId, cancellationToken)
            ?? throw new AdministrationNotFoundException("The court does not exist.");
        authorizer.RequireSiteAccess(actor, court.SiteId);
        return await repository.UpdateCourtAsync(courtId, ValidateCourtInput(input), cancellationToken);
    }

    public async Task<SiteAnnualSchedule> SetScheduleAsync(
        string actorMatricule,
        int siteId,
        int calendarYear,
        ScheduleInput input,
        CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        await RequireSiteAsync(actor, siteId, cancellationToken);
        ValidateScheduleInput(calendarYear, input);

        if (await repository.HasMatchOutsideScheduleAsync(
                siteId,
                calendarYear,
                input.OpeningTime,
                input.ClosingTime,
                cancellationToken))
        {
            throw new AdministrationConflictException(
                "The schedule would make an existing match fall outside the site's opening hours.");
        }

        return await repository.SetScheduleAsync(siteId, calendarYear, input, cancellationToken);
    }

    public async Task<IReadOnlyList<SiteAnnualSchedule>> GetSchedulesAsync(
        string actorMatricule,
        int siteId,
        CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        await RequireSiteAsync(actor, siteId, cancellationToken);
        return await repository.GetSchedulesAsync(siteId, cancellationToken);
    }

    public async Task DeleteScheduleAsync(
        string actorMatricule,
        int siteId,
        int calendarYear,
        CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        await RequireSiteAsync(actor, siteId, cancellationToken);
        await repository.DeleteScheduleAsync(siteId, calendarYear, cancellationToken);
    }

    public async Task<IReadOnlyList<Closure>> GetClosuresAsync(string actorMatricule, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        return await repository.GetClosuresAsync(
            actor.Scope == AdministratorScope.Site ? actor.SiteId : null,
            cancellationToken);
    }

    public async Task<Closure> CreateClosureAsync(string actorMatricule, ClosureInput input, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        var normalizedInput = ValidateClosureInput(input);
        await AuthorizeClosureAsync(actor, normalizedInput, cancellationToken);
        await EnsureClosureDoesNotConflictAsync(normalizedInput, cancellationToken);
        return await repository.CreateClosureAsync(normalizedInput, cancellationToken);
    }

    public async Task<Closure> UpdateClosureAsync(
        string actorMatricule,
        int closureId,
        ClosureInput input,
        CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        var existingClosure = await repository.GetClosureAsync(closureId, cancellationToken)
            ?? throw new AdministrationNotFoundException("The closure does not exist.");
        await AuthorizeClosureAsync(actor, ToInput(existingClosure), cancellationToken);

        var normalizedInput = ValidateClosureInput(input);
        await AuthorizeClosureAsync(actor, normalizedInput, cancellationToken);
        await EnsureClosureDoesNotConflictAsync(normalizedInput, cancellationToken);
        return await repository.UpdateClosureAsync(closureId, normalizedInput, cancellationToken);
    }

    public async Task DeleteClosureAsync(string actorMatricule, int closureId, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorMatricule, cancellationToken);
        var closure = await repository.GetClosureAsync(closureId, cancellationToken)
            ?? throw new AdministrationNotFoundException("The closure does not exist.");
        await AuthorizeClosureAsync(actor, ToInput(closure), cancellationToken);
        await repository.DeleteClosureAsync(closureId, cancellationToken);
    }

    private async Task<AdministratorActor> GetActorAsync(string actorMatricule, CancellationToken cancellationToken)
    {
        var normalizedMatricule = NormalizeMatricule(actorMatricule);
        return await repository.GetActiveAdministratorAsync(normalizedMatricule, cancellationToken)
            ?? throw new AdministrationForbiddenException("The acting matricule is not an active administrator.");
    }

    private async Task<Member> GetMemberAsync(string matricule, CancellationToken cancellationToken)
    {
        var normalizedMatricule = NormalizeMatricule(matricule);
        return await repository.GetMemberByMatriculeAsync(normalizedMatricule, cancellationToken)
            ?? throw new AdministrationNotFoundException("The member does not exist.");
    }

    private async Task RequireSiteAsync(AdministratorActor actor, int siteId, CancellationToken cancellationToken)
    {
        _ = await repository.GetSiteAsync(siteId, cancellationToken)
            ?? throw new AdministrationNotFoundException("The site does not exist.");
        authorizer.RequireSiteAccess(actor, siteId);
    }

    private async Task AuthorizeMemberInputAsync(
        AdministratorActor actor,
        MemberInput input,
        CancellationToken cancellationToken)
    {
        if (actor.Scope == AdministratorScope.Global)
        {
            return;
        }

        if (input.MembershipCategory != MembershipCategory.Site || input.HomeSiteId != actor.SiteId)
        {
            throw new AdministrationForbiddenException(
                "A site administrator can create or update only members of the assigned site.");
        }

    }

    private async Task ValidateMemberHomeSiteAsync(MemberInput input, CancellationToken cancellationToken)
    {
        if (input.HomeSiteId is null)
        {
            return;
        }

        _ = await repository.GetSiteAsync(input.HomeSiteId.Value, cancellationToken)
            ?? throw new AdministrationNotFoundException("The member home site does not exist.");
    }

    private async Task<bool> IsActiveGlobalAdministratorAsync(Member member, CancellationToken cancellationToken)
    {
        if (!member.IsActive)
        {
            return false;
        }

        var role = await repository.GetActiveAdministratorAsync(member.Matricule, cancellationToken);
        return role?.Scope == AdministratorScope.Global;
    }

    private async Task EnsureActiveGlobalAdministratorRemainsAsync(CancellationToken cancellationToken)
    {
        if (await repository.GetActiveGlobalAdministratorCountAsync(cancellationToken) <= 1)
        {
            throw new AdministrationConflictException(
                "At least one active global administrator must remain assigned.");
        }
    }

    private async Task ValidateRoleInputAsync(AdministratorRoleInput input, CancellationToken cancellationToken)
    {
        if (input.Scope == AdministratorScope.Global && input.SiteId is null)
        {
            return;
        }

        if (input.Scope != AdministratorScope.Site || input.SiteId is null)
        {
            throw new AdministrationValidationException(
                "A global role must not have a site, and a site role must have exactly one site.");
        }

        _ = await repository.GetSiteAsync(input.SiteId.Value, cancellationToken)
            ?? throw new AdministrationNotFoundException("The administrator site does not exist.");
    }

    private async Task AuthorizeClosureAsync(
        AdministratorActor actor,
        ClosureInput input,
        CancellationToken cancellationToken)
    {
        if (input.Scope == AdministratorScope.Global)
        {
            authorizer.RequireGlobal(actor);
            return;
        }

        await RequireSiteAsync(actor, input.SiteId!.Value, cancellationToken);
    }

    private async Task EnsureClosureDoesNotConflictAsync(
        ClosureInput input,
        CancellationToken cancellationToken)
    {
        if (await repository.HasMatchOverlappingClosureAsync(input, cancellationToken))
        {
            throw new AdministrationConflictException("A closure cannot overlap an existing match.");
        }
    }

    private static string NormalizeMatricule(string matricule)
    {
        if (string.IsNullOrWhiteSpace(matricule))
        {
            throw new AdministrationValidationException("A matricule is required.");
        }

        return matricule.Trim().ToUpperInvariant();
    }

    private static MemberInput ValidateMemberInput(MemberInput input)
    {
        var matricule = NormalizeMatricule(input.Matricule);
        var displayName = input.DisplayName?.Trim();

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new AdministrationValidationException("A member display name is required.");
        }

        var valid = input.MembershipCategory switch
        {
            MembershipCategory.Global => matricule.Length == 5
                && matricule[0] == 'G'
                && matricule[1..].All(char.IsDigit)
                && input.HomeSiteId is null,
            MembershipCategory.Site => matricule.Length == 6
                && matricule[0] == 'S'
                && matricule[1..].All(char.IsDigit)
                && input.HomeSiteId is not null,
            MembershipCategory.Free => matricule.Length == 6
                && matricule[0] == 'L'
                && matricule[1..].All(char.IsDigit)
                && input.HomeSiteId is null,
            _ => false
        };

        if (!valid)
        {
            throw new AdministrationValidationException(
                "The membership category, matricule format, and home site are inconsistent.");
        }

        return input with { Matricule = matricule, DisplayName = displayName };
    }

    private static SiteInput ValidateSiteInput(SiteInput input)
    {
        var name = input.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new AdministrationValidationException("A site name is required.");
        }

        return input with { Name = name };
    }

    private static CourtInput ValidateCourtInput(CourtInput input)
    {
        var name = input.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new AdministrationValidationException("A court name is required.");
        }

        return input with { Name = name };
    }

    private static void ValidateScheduleInput(int calendarYear, ScheduleInput input)
    {
        if (calendarYear is < 2000 or > 9999 || input.OpeningTime >= input.ClosingTime)
        {
            throw new AdministrationValidationException(
                "The calendar year and opening hours are invalid.");
        }
    }

    private static ClosureInput ValidateClosureInput(ClosureInput input)
    {
        var reason = input.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || input.StartsAt >= input.EndsAt)
        {
            throw new AdministrationValidationException("The closure reason or period is invalid.");
        }

        var hasExpectedSite = (input.Scope == AdministratorScope.Global && input.SiteId is null)
            || (input.Scope == AdministratorScope.Site && input.SiteId is not null);
        if (!hasExpectedSite)
        {
            throw new AdministrationValidationException(
                "A global closure must not have a site, and a site closure must have exactly one site.");
        }

        return input with { Reason = reason };
    }

    private static ClosureInput ToInput(Closure closure) =>
        new(closure.Scope, closure.SiteId, closure.StartsAt, closure.EndsAt, closure.Reason);
}
