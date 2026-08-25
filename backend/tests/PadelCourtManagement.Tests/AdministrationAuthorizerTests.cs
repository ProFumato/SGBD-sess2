using PadelCourtManagement.Application.Administration;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Tests;

public sealed class AdministrationAuthorizerTests
{
    private readonly AdministrationAuthorizer authorizer = new();

    [Fact]
    public void GlobalAdministratorCanAccessEverySite()
    {
        var actor = new AdministratorActor(1, "G0001", AdministratorScope.Global, null);

        authorizer.RequireSiteAccess(actor, siteId: 42);
    }

    [Fact]
    public void SiteAdministratorCanAccessOnlyAssignedSite()
    {
        var actor = new AdministratorActor(2, "S00001", AdministratorScope.Site, SiteId: 4);

        authorizer.RequireSiteAccess(actor, siteId: 4);

        Assert.Throws<AdministrationForbiddenException>(
            () => authorizer.RequireSiteAccess(actor, siteId: 5));
    }

    [Fact]
    public void SiteAdministratorCanManageOnlyMembersOfAssignedSite()
    {
        var actor = new AdministratorActor(2, "S00001", AdministratorScope.Site, SiteId: 4);
        var assignedMember = new Member(3, "S00002", "Assigned member", MembershipCategory.Site, 4, true);
        var globalMember = new Member(4, "G0002", "Global member", MembershipCategory.Global, null, true);

        authorizer.RequireMemberAccess(actor, assignedMember);

        Assert.Throws<AdministrationForbiddenException>(
            () => authorizer.RequireMemberAccess(actor, globalMember));
    }

    [Fact]
    public void OnlyGlobalAdministratorCanManageAdministratorRoles()
    {
        var siteActor = new AdministratorActor(2, "S00001", AdministratorScope.Site, SiteId: 4);

        Assert.Throws<AdministrationForbiddenException>(
            () => authorizer.RequireGlobal(siteActor));
    }
}
