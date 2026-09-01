// Debt Service Contract: Interface definition.
// Defines methods for viewing and clearing member debts.

using PadelCourtManagement.Domain;
using PadelCourtManagement.Application.Administration;

namespace PadelCourtManagement.Application;

public interface IDebtService
{
    Task<IReadOnlyList<MemberDebt>> GetOutstandingDebtsAsync(
        string matricule,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<MemberDebt>> GetDebtsForAdministratorAsync(
        string actorMatricule,
        string memberMatricule,
        CancellationToken cancellationToken);
    Task ClearDebtsForAdministratorAsync(
        string actorMatricule,
        string memberMatricule,
        CancellationToken cancellationToken);
}

public interface IDebtRepository
{
    Task<ReservationMember?> GetMemberAsync(string matricule, CancellationToken cancellationToken);
    Task<IReadOnlyList<MemberDebt>> GetOutstandingDebtsAsync(int memberId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MemberDebt>> GetDebtsForAdministratorAsync(
        int memberId,
        AdministratorScope scope,
        int? siteId,
        CancellationToken cancellationToken);
    Task ClearDebtsForAdministratorAsync(
        int memberId,
        AdministratorScope scope,
        int? siteId,
        CancellationToken cancellationToken);
}
