namespace PadelCourtManagement.Domain;

public sealed record MemberDebt(
    int DebtId,
    int MatchId,
    string CourtName,
    int SiteId,
    DateTime StartsAt,
    decimal InitialAmount,
    decimal OutstandingAmount);
