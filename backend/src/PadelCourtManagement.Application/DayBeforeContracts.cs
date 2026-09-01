// Day Before Service Contract: Interface definition.
// Defines method for running daily processing tasks (match publishing, bans, fees).

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
