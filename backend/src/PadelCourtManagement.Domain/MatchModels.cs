namespace PadelCourtManagement.Domain;

public sealed record PrivateParticipantInput(string OrganizerMatricule, string ParticipantMatricule);

public sealed record PublicMatch(
    int MatchId,
    int CourtId,
    string CourtName,
    int SiteId,
    DateTime StartsAt,
    DateTime EndsAt,
    int AvailablePlaces);

public sealed record PublicMatchJoinResult(int MatchId, int MatchParticipantId, int PaymentId);

public sealed record MatchDetails(
    int MatchId,
    int OrganizerMemberId,
    ReservationVisibility Visibility,
    DateTime StartsAt);

public sealed record MatchParticipantDetails(
    int MatchParticipantId,
    int MemberId,
    string Matricule,
    string DisplayName,
    bool IsOrganizer,
    string ParticipationStatus,
    bool IsPaid);
