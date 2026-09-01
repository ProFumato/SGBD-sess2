// Debt Models: Data type for member outstanding balances.
// Represents unpaid fees for a specific match/reservation.

namespace PadelCourtManagement.Domain;

public sealed record MemberDebt(
    int DebtId,
    int MatchId,
    string CourtName,
    int SiteId,
    DateTime StartsAt,
    decimal InitialAmount,
    decimal OutstandingAmount);
