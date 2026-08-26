using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

public interface IMatchService
{
    Task AddPrivateParticipantAsync(int matchId, PrivateParticipantInput input, CancellationToken cancellationToken);
    Task<IReadOnlyList<PublicMatch>> GetPublicMatchesAsync(string matricule, CancellationToken cancellationToken);
    Task<PublicMatchJoinResult> JoinPublicMatchAsync(int matchId, string matricule, CancellationToken cancellationToken);
}

public interface IMatchRepository
{
    Task<ReservationMember?> GetMemberAsync(string matricule, CancellationToken cancellationToken);
    Task<MatchDetails?> GetMatchAsync(int matchId, CancellationToken cancellationToken);
    Task AddPrivateParticipantAsync(int matchId, int organizerMemberId, int participantMemberId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PublicMatch>> GetPublicMatchesAsync(DateTime now, CancellationToken cancellationToken);
    Task<PublicMatchJoinResult> JoinPublicMatchAsync(int matchId, int memberId, DateTime paidAt, CancellationToken cancellationToken);
}
