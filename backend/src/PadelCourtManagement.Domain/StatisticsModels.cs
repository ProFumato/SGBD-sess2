namespace PadelCourtManagement.Domain;

public sealed record StatisticsRequest(
    DateTime From,
    DateTime To,
    int? SiteId);

public sealed record StatisticsReport(
    DateTime From,
    DateTime To,
    decimal Revenue,
    int Matches,
    int ConfirmedParticipations,
    int Capacity,
    int ActiveMembers,
    decimal OutstandingDebt,
    int ActiveBookingBans,
    IReadOnlyList<StatisticsBreakdown> Breakdown);

public sealed record StatisticsBreakdown(
    int SiteId,
    string SiteName,
    int CourtId,
    string CourtName,
    int Matches,
    int ConfirmedParticipations,
    decimal Revenue);
