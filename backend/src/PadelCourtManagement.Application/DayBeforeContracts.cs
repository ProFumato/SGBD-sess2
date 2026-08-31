using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

public interface IDayBeforeService
{
    Task<DayBeforeProcessingResult> ProcessAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public interface IDayBeforeRepository
{
    Task<IReadOnlyList<int>> GetTomorrowMatchIdsAsync(
        DateOnly tomorrow,
        CancellationToken cancellationToken);
    Task<DayBeforeMatchResult> ProcessMatchAsync(
        int matchId,
        DateTime now,
        CancellationToken cancellationToken);
}
