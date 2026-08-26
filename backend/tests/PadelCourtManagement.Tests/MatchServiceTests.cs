using PadelCourtManagement.Application;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Tests;

public sealed class MatchServiceTests
{
    [Fact]
    public async Task OnlyPrivateOrganizerCanAddParticipant()
    {
        var repository = new FakeMatchRepository
        {
            Match = new MatchDetails(5, 1, ReservationVisibility.Private, DateTime.UtcNow.AddDays(2))
        };
        var service = new MatchService(repository);

        await service.AddPrivateParticipantAsync(
            5,
            new PrivateParticipantInput("G0001", "G0002"),
            CancellationToken.None);

        Assert.Equal(5, repository.AddedParticipant!.Value.MatchId);
        Assert.Equal(1, repository.AddedParticipant!.Value.OrganizerId);
        Assert.Equal(2, repository.AddedParticipant!.Value.ParticipantId);
    }

    [Fact]
    public async Task PublicMatchRejectsOrganizerAddedParticipant()
    {
        var repository = new FakeMatchRepository
        {
            Match = new MatchDetails(5, 1, ReservationVisibility.Public, DateTime.UtcNow.AddDays(2))
        };
        var service = new MatchService(repository);

        await Assert.ThrowsAsync<ReservationForbiddenException>(() =>
            service.AddPrivateParticipantAsync(
                5,
                new PrivateParticipantInput("G0001", "G0002"),
                CancellationToken.None));
    }

    [Fact]
    public async Task OrganizerCanListPrivateParticipants()
    {
        var repository = new FakeMatchRepository
        {
            Match = new MatchDetails(5, 1, ReservationVisibility.Private, DateTime.UtcNow.AddDays(2)),
            Participants = [new MatchParticipantDetails(8, 2, "G0002", "Player", false, "Pending", false)]
        };
        var service = new MatchService(repository);

        var result = await service.GetPrivateParticipantsAsync(5, "G0001", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(8, result[0].MatchParticipantId);
    }

    [Fact]
    public async Task NonOrganizerCannotRemovePrivateParticipant()
    {
        var repository = new FakeMatchRepository
        {
            Match = new MatchDetails(5, 1, ReservationVisibility.Private, DateTime.UtcNow.AddDays(2))
        };
        var service = new MatchService(repository);

        await Assert.ThrowsAsync<ReservationForbiddenException>(() =>
            service.RemovePrivateParticipantAsync(5, 8, "G0002", CancellationToken.None));
    }

    private sealed class FakeMatchRepository : IMatchRepository
    {
        public MatchDetails? Match { get; init; }
        public IReadOnlyList<MatchParticipantDetails> Participants { get; init; } = [];
        public (int MatchId, int OrganizerId, int ParticipantId)? AddedParticipant { get; private set; }

        public Task<ReservationMember?> GetMemberAsync(string matricule, CancellationToken cancellationToken) =>
            Task.FromResult<ReservationMember?>(
                matricule == "G0001"
                    ? new ReservationMember(1, MembershipCategory.Global, null, true)
                    : new ReservationMember(2, MembershipCategory.Global, null, true));

        public Task<MatchDetails?> GetMatchAsync(int matchId, CancellationToken cancellationToken) =>
            Task.FromResult(Match);

        public Task AddPrivateParticipantAsync(int matchId, int organizerMemberId, int participantMemberId, CancellationToken cancellationToken)
        {
            AddedParticipant = (matchId, organizerMemberId, participantMemberId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MatchParticipantDetails>> GetPrivateParticipantsAsync(
            int matchId,
            int organizerMemberId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Participants);

        public Task<IReadOnlyList<PrivateMatchOverview>> GetPrivateMatchesAsync(
            int memberId,
            DateTime now,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PrivateMatchOverview>>(Array.Empty<PrivateMatchOverview>());

        public Task RemovePrivateParticipantAsync(
            int matchId,
            int participantId,
            int organizerMemberId,
            DateTime now,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ReplacePrivateParticipantAsync(
            int matchId,
            int participantId,
            int organizerMemberId,
            int replacementMemberId,
            DateTime now,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<PublicMatch>> GetPublicMatchesAsync(DateTime now, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PublicMatch>>(Array.Empty<PublicMatch>());

        public Task<PublicMatchJoinResult> JoinPublicMatchAsync(int matchId, int memberId, DateTime paidAt, CancellationToken cancellationToken) =>
            Task.FromResult(new PublicMatchJoinResult(matchId, 1, 1));
    }
}
