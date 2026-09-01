// Statistics Service Contract: Interface definition.
// Defines method for retrieving usage reports and statistics.

using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

public interface IStatisticsService
{
    Task<StatisticsReport> GetAsync(
        string actorMatricule,
        StatisticsRequest request,
        CancellationToken cancellationToken);
}

public interface IStatisticsRepository
{
    Task<StatisticsReport> GetAsync(
        StatisticsRequest request,
        CancellationToken cancellationToken);
}
