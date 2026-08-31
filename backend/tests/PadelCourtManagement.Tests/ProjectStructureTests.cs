using PadelCourtManagement.Application;
using PadelCourtManagement.Domain;
using PadelCourtManagement.Infrastructure;

namespace PadelCourtManagement.Tests;

public sealed class ProjectStructureTests
{
    [Fact]
    public void BackendLayersExposeTheirAssemblyMarkers()
    {
        Assert.Equal("PadelCourtManagement.Domain", typeof(DomainAssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("PadelCourtManagement.Application", typeof(ApplicationAssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("PadelCourtManagement.Infrastructure", typeof(InfrastructureAssemblyMarker).Assembly.GetName().Name);
    }
}
