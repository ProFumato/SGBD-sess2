namespace PadelCourtManagement.Domain;

public enum ReservationVisibility
{
    Private,
    Public
}

public sealed record AvailabilityRequest(
    string Matricule,
    string SiteCode,
    DateOnly Date,
    TimeOnly StartTime);

public sealed record AvailableSlot(
    string CourtCode,
    DateTimeOffset Start,
    DateTimeOffset End);

public sealed record ReservationRequest(
    string Matricule,
    string CourtCode,
    DateTimeOffset Start,
    ReservationVisibility Visibility);

public sealed record ReservationResult(
    string ReservationCode,
    string CourtCode,
    DateTimeOffset Start,
    DateTimeOffset End);
