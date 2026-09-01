// Statistics Service: Generates usage reports for admins.
// Provides court usage, match counts, and revenue data for specified date ranges and sites.

using PadelCourtManagement.Application.Administration;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

public sealed class StatisticsService(
    IStatisticsRepository repository,
    IAdministratorRepository administrators,
    ISiteRepository sites,
    AdministrationAuthorizer authorizer) : IStatisticsService
{
    public async Task<StatisticsReport> GetAsync(
        string actorMatricule,
        StatisticsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.From >= request.To)
        {
            throw new AdministrationValidationException("The statistics start must be before the end.");
        }

        var actor = await administrators.GetActiveAdministratorAsync(
            Normalize(actorMatricule),
            cancellationToken)
            ?? throw new AdministrationForbiddenException("The acting matricule is not an active administrator.");

        if (request.SiteId is <= 0)
        {
            throw new AdministrationValidationException("The site identifier must be positive.");
        }

        if (request.SiteId is not null)
        {
            _ = await sites.GetSiteAsync(request.SiteId.Value, cancellationToken)
                ?? throw new AdministrationNotFoundException("The statistics site does not exist.");
            authorizer.RequireSiteAccess(actor, request.SiteId.Value);
        }

        var scopedRequest = actor.Scope == AdministratorScope.Site
            ? request with { SiteId = actor.SiteId }
            : request;
        return await repository.GetAsync(scopedRequest, cancellationToken);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
