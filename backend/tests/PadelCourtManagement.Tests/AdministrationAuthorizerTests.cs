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

        authorizer.RequireSiteAccess(actor, 42);
    }

    [Fact]
    public void SiteAdministratorCanAccessOnlyAssignedSite()
    {
        var actor = new AdministratorActor(2, "S00001", AdministratorScope.Site, 4);

        authorizer.RequireSiteAccess(actor, 4);

        Assert.Throws<AdministrationForbiddenException>(() => authorizer.RequireSiteAccess(actor, 5));
    }

    [Fact]
    public void SiteAdministratorCanManageOnlyAssignedSiteMembers()
    {
        var actor = new AdministratorActor(2, "S00001", AdministratorScope.Site, 4);
        var assignedMember = new Member(3, "S00002", "Assigned", MembershipCategory.Site, 4, true);
        var globalMember = new Member(4, "G0002", "Global", MembershipCategory.Global, null, true);

        authorizer.RequireMemberAccess(actor, assignedMember);
        Assert.Throws<AdministrationForbiddenException>(() => authorizer.RequireMemberAccess(actor, globalMember));
    }

    [Fact]
    public void OnlyGlobalAdministratorCanManageRoles()
    {
        var actor = new AdministratorActor(2, "S00001", AdministratorScope.Site, 4);

        Assert.Throws<AdministrationForbiddenException>(() => authorizer.RequireGlobal(actor));
    }
}
