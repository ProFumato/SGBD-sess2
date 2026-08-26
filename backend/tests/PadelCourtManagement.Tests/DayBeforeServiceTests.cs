using PadelCourtManagement.Application;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Tests;

public sealed class DayBeforeServiceTests
{
    [Fact]
    public async Task ProcessesTomorrowMatchesAndAggregatesResults()
    {
        var now = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.FromHours(2));
        var repository = new FakeDayBeforeRepository
        {
            MatchIds = [4, 5],
            Results =
            [
                new DayBeforeMatchResult(true, 2, true, true),
                new DayBeforeMatchResult(false, 0, false, false)
            ]
        };

        var result = await new DayBeforeService(repository).ProcessAsync(now, CancellationToken.None);

        Assert.Equal(2, result.MatchesProcessed);
        Assert.Equal(1, result.MatchesPublished);
        Assert.Equal(2, result.ParticipantsRemoved);
        Assert.Equal(1, result.BansCreated);
        Assert.Equal(1, result.DebtsCreated);
        Assert.Equal(new DateOnly(2026, 8, 27), repository.RequestedDate);
    }

    private sealed class FakeDayBeforeRepository : IDayBeforeRepository
    {
        public IReadOnlyList<int> MatchIds { get; init; } = [];
        public IReadOnlyList<DayBeforeMatchResult> Results { get; init; } = [];
        public DateOnly RequestedDate { get; private set; }

        public Task<IReadOnlyList<int>> GetTomorrowMatchIdsAsync(
            DateOnly tomorrow,
            CancellationToken cancellationToken)
        {
            RequestedDate = tomorrow;
            return Task.FromResult(MatchIds);
        }

        public Task<DayBeforeMatchResult> ProcessMatchAsync(
            int matchId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            var index = MatchIds.ToList().IndexOf(matchId);
            return Task.FromResult(Results[index]);
        }
    }
}
