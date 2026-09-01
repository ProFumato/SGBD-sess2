// Day Before Models: Data types for daily processing results.
// Tracks how many matches were published, players removed, bans created, and debts added each day.

namespace PadelCourtManagement.Domain;

public sealed record DayBeforeProcessingResult(
    int MatchesProcessed,
    int MatchesPublished,
    int ParticipantsRemoved,
    int BansCreated,
    int DebtsCreated);

public sealed record DayBeforeMatchResult(
    bool Published,
    int ParticipantsRemoved,
    bool BanCreated,
    bool DebtCreated);
