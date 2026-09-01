namespace PadelCourtManagement.Domain;

public enum ReservationVisibility
{
    Private,
    Public
}

// These records are also the HTTP request/response contracts in this project; there is no separate API mapping layer.
public sealed record AvailabilityRequest(
    string Matricule,
    int SiteId,
    DateOnly Date);

public sealed record AvailableSlot(
    int CourtId,
    string CourtName,
    DateTime StartAt,
    DateTime EndAt);

public sealed record ReservationRequest(
    string Matricule,
    int CourtId,
    DateOnly Date,
    TimeOnly StartTime,
    ReservationVisibility Visibility);

public sealed record ReservationResult(
    int MatchId,
    int CourtId,
    DateTime StartAt,
    DateTime EndAt,
    ReservationVisibility Visibility);

public sealed record ReservationMember(
    int MemberId,
    MembershipCategory MembershipCategory,
    int? HomeSiteId,
    bool IsActive);

public sealed record ReservationContext(
    ReservationMember Member,
    int CourtId,
    int SiteId,
    bool IsCourtActive,
    TimeOnly? OpeningTime,
    TimeOnly? ClosingTime,
    bool HasOverlappingClosure,
    bool HasActiveDebt,
    bool HasActiveBookingBan);

public sealed record ReservationCommand(
    int MemberId,
    int CourtId,
    DateTime StartAt,
    ReservationVisibility Visibility,
    DateTime Now);
