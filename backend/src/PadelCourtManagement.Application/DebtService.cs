using PadelCourtManagement.Domain;
using PadelCourtManagement.Application.Administration;

namespace PadelCourtManagement.Application;

public sealed class DebtService(IDebtRepository repository, IAdministratorRepository administrators) : IDebtService
{
    public async Task<IReadOnlyList<MemberDebt>> GetOutstandingDebtsAsync(
        string matricule,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(matricule))
        {
            throw new ReservationValidationException("The matricule is required.");
        }

        var member = await repository.GetMemberAsync(matricule.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new ReservationNotFoundException("The matricule does not identify a member.");
        if (!member.IsActive)
        {
            throw new ReservationForbiddenException("Inactive members cannot view debts.");
        }

        return await repository.GetOutstandingDebtsAsync(member.MemberId, cancellationToken);
    }

    public async Task<IReadOnlyList<MemberDebt>> GetDebtsForAdministratorAsync(
        string actorMatricule,
        string memberMatricule,
        CancellationToken cancellationToken)
    {
        var actor = await administrators.GetActiveAdministratorAsync(
            actorMatricule.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new AdministrationForbiddenException("The acting matricule is not an active administrator.");
        var member = await GetMemberAsync(memberMatricule, cancellationToken);
        return await repository.GetDebtsForAdministratorAsync(
            member.MemberId, actor.Scope, actor.SiteId, cancellationToken);
    }

    public async Task ClearDebtsForAdministratorAsync(
        string actorMatricule,
        string memberMatricule,
        CancellationToken cancellationToken)
    {
        var actor = await administrators.GetActiveAdministratorAsync(
            actorMatricule.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new AdministrationForbiddenException("The acting matricule is not an active administrator.");
        var member = await GetMemberAsync(memberMatricule, cancellationToken);
        await repository.ClearDebtsForAdministratorAsync(
            member.MemberId, actor.Scope, actor.SiteId, cancellationToken);
    }

    private async Task<ReservationMember> GetMemberAsync(string matricule, CancellationToken cancellationToken)
    {
        var member = await repository.GetMemberAsync(matricule.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new AdministrationNotFoundException("The member does not exist.");
        return member;
    }
}
