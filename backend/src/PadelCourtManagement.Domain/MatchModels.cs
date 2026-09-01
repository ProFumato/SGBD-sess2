// Match Models: Data types for public and private matches.
// Represents match info, participants, and organizer details.

namespace PadelCourtManagement.Domain;

public sealed record PrivateParticipantInput(string OrganizerMatricule, string ParticipantMatricule);

public sealed record PublicMatchParticipant(
    int MemberId,
    string Matricule,
    string DisplayName);

public sealed record PublicMatch(
    int MatchId,
    int CourtId,
    string CourtName,
    int SiteId,
    DateTime StartsAt,
    DateTime EndsAt,
    int AvailablePlaces,
    IReadOnlyList<PublicMatchParticipant> Participants);

public sealed record PublicMatchJoinResult(int MatchId, int MatchParticipantId, int PaymentId);

public sealed record MatchDetails(
    int MatchId,
    int OrganizerMemberId,
    ReservationVisibility Visibility,
    DateTime StartsAt);

public sealed record PrivateMatchOverview(
    int MatchId,
    int CourtId,
    string CourtName,
    int SiteId,
    string SiteName,
    DateTime StartsAt,
    DateTime EndsAt,
    IReadOnlyList<MatchParticipantDetails> Participants);

public sealed record MatchParticipantDetails(
    int MatchParticipantId,
    int MemberId,
    string Matricule,
    string DisplayName,
    bool IsOrganizer,
    string ParticipationStatus,
    bool IsPaid);
