// Match Service Contract: Interface definition.
// Defines methods for managing match participants and retrieving match data.

using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

public interface IMatchService
{
    Task AddPrivateParticipantAsync(int matchId, PrivateParticipantInput input, CancellationToken cancellationToken);
    Task<IReadOnlyList<MatchParticipantDetails>> GetPrivateParticipantsAsync(
        int matchId,
        string matricule,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<PrivateMatchOverview>> GetPrivateMatchesAsync(
        string matricule,
        CancellationToken cancellationToken);
    Task RemovePrivateParticipantAsync(
        int matchId,
        int participantId,
        string organizerMatricule,
        CancellationToken cancellationToken);
    Task ReplacePrivateParticipantAsync(
        int matchId,
        int participantId,
        PrivateParticipantInput input,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<PublicMatch>> GetPublicMatchesAsync(string matricule, CancellationToken cancellationToken);
    Task<PublicMatchJoinResult> JoinPublicMatchAsync(int matchId, string matricule, CancellationToken cancellationToken);
}

public interface IMatchRepository
{
    Task<ReservationMember?> GetMemberAsync(string matricule, CancellationToken cancellationToken);
    Task<MatchDetails?> GetMatchAsync(int matchId, CancellationToken cancellationToken);
    Task AddPrivateParticipantAsync(int matchId, int organizerMemberId, int participantMemberId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MatchParticipantDetails>> GetPrivateParticipantsAsync(
        int matchId,
        int memberId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<PrivateMatchOverview>> GetPrivateMatchesAsync(
        int memberId,
        DateTime now,
        CancellationToken cancellationToken);
    Task RemovePrivateParticipantAsync(
        int matchId,
        int participantId,
        int organizerMemberId,
        DateTime now,
        CancellationToken cancellationToken);
    Task ReplacePrivateParticipantAsync(
        int matchId,
        int participantId,
        int organizerMemberId,
        int replacementMemberId,
        DateTime now,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<PublicMatch>> GetPublicMatchesAsync(int memberId, DateTime now, CancellationToken cancellationToken);
    Task<PublicMatchJoinResult> JoinPublicMatchAsync(int matchId, int memberId, DateTime paidAt, CancellationToken cancellationToken);
}
