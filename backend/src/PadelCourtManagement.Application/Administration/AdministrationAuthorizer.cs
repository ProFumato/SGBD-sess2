// Administration Authorizer: Enforces role-based access control.
// Checks if admins have Global scope or Site-specific permissions for each operation.

using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application.Administration;

public sealed class AdministrationAuthorizer
{
    public void RequireGlobal(AdministratorActor actor)
    {
        if (actor.Scope != AdministratorScope.Global)
        {
            throw new AdministrationForbiddenException("This operation requires a global administrator.");
        }
    }

    public void RequireSiteAccess(AdministratorActor actor, int siteId)
    {
        if (actor.Scope == AdministratorScope.Global)
        {
            return;
        }

        if (actor.SiteId != siteId)
        {
            throw new AdministrationForbiddenException("The administrator is not assigned to this site.");
        }
    }

    public void RequireMemberAccess(AdministratorActor actor, Member member)
    {
        if (actor.Scope == AdministratorScope.Global)
        {
            return;
        }

        if (member.MembershipCategory != MembershipCategory.Site || member.HomeSiteId != actor.SiteId)
        {
            throw new AdministrationForbiddenException("A site administrator can manage only members of the assigned site.");
        }
    }
}
