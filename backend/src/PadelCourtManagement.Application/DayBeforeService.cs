using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

// Chooses the local "tomorrow" once, then lets the repository process each matching record.
public sealed class DayBeforeService(IDayBeforeRepository repository) : IDayBeforeService
{
    private static readonly TimeZoneInfo BrusselsTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");

    public async Task<DayBeforeProcessingResult> ProcessAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // The business date follows the project site timezone, not the machine or UTC calendar date.
        var brusselsNow = TimeZoneInfo.ConvertTime(now, BrusselsTimeZone);
        var matchIds = await repository.GetTomorrowMatchIdsAsync(
            DateOnly.FromDateTime(brusselsNow.DateTime).AddDays(1),
            cancellationToken);

        var published = 0;
        var removed = 0;
        var bans = 0;
        var debts = 0;

        foreach (var matchId in matchIds)
        {
            var result = await repository.ProcessMatchAsync(
                matchId,
                brusselsNow.DateTime,
                cancellationToken);
            published += result.Published ? 1 : 0;
            removed += result.ParticipantsRemoved;
            bans += result.BanCreated ? 1 : 0;
            debts += result.DebtCreated ? 1 : 0;
        }

        return new DayBeforeProcessingResult(
            matchIds.Count,
            published,
            removed,
            bans,
            debts);
    }
}
