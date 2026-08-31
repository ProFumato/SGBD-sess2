using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

public sealed class MatchService(IMatchRepository repository) : IMatchService
{
    public async Task AddPrivateParticipantAsync(
        int matchId,
        PrivateParticipantInput input,
        CancellationToken cancellationToken)
    {
        if (matchId <= 0)
        {
            throw new ReservationValidationException("A valid match identifier is required.");
        }

        var organizer = await GetActiveMemberAsync(input.OrganizerMatricule, cancellationToken);
        var participant = await GetActiveMemberAsync(input.ParticipantMatricule, cancellationToken);
        if (organizer.MemberId == participant.MemberId)
        {
            throw new ReservationValidationException("The organizer is already a participant.");
        }

        var match = await repository.GetMatchAsync(matchId, cancellationToken)
            ?? throw new ReservationNotFoundException("The match does not exist.");
        if (match.Visibility != ReservationVisibility.Private)
        {
            throw new ReservationForbiddenException("Only private matches accept organizer-added participants.");
        }

        if (match.OrganizerMemberId != organizer.MemberId)
        {
            throw new ReservationForbiddenException("Only the private match organizer can add participants.");
        }

        await repository.AddPrivateParticipantAsync(matchId, organizer.MemberId, participant.MemberId, cancellationToken);
    }

    public async Task<IReadOnlyList<MatchParticipantDetails>> GetPrivateParticipantsAsync(
        int matchId,
        string matricule,
        CancellationToken cancellationToken)
    {
        var member = await GetActiveMemberAsync(matricule, cancellationToken);
        var match = await GetPrivateMatchAsync(matchId, cancellationToken);
        return await repository.GetPrivateParticipantsAsync(matchId, member.MemberId, cancellationToken);
    }

    public async Task<IReadOnlyList<PrivateMatchOverview>> GetPrivateMatchesAsync(
        string matricule,
        CancellationToken cancellationToken)
    {
        var member = await GetActiveMemberAsync(matricule, cancellationToken);
        return await repository.GetPrivateMatchesAsync(member.MemberId, DateTime.UtcNow, cancellationToken);
    }

    public async Task RemovePrivateParticipantAsync(
        int matchId,
        int participantId,
        string organizerMatricule,
        CancellationToken cancellationToken)
    {
        if (participantId <= 0)
        {
            throw new ReservationValidationException("A valid participant identifier is required.");
        }

        var organizer = await GetActiveMemberAsync(organizerMatricule, cancellationToken);
        var match = await GetPrivateMatchAsync(matchId, cancellationToken);
        RequireOrganizer(match, organizer.MemberId);
        await repository.RemovePrivateParticipantAsync(
            matchId,
            participantId,
            organizer.MemberId,
            DateTime.UtcNow,
            cancellationToken);
    }

    public async Task ReplacePrivateParticipantAsync(
        int matchId,
        int participantId,
        PrivateParticipantInput input,
        CancellationToken cancellationToken)
    {
        if (participantId <= 0)
        {
            throw new ReservationValidationException("A valid participant identifier is required.");
        }

        var organizer = await GetActiveMemberAsync(input.OrganizerMatricule, cancellationToken);
        var replacement = await GetActiveMemberAsync(input.ParticipantMatricule, cancellationToken);
        if (organizer.MemberId == replacement.MemberId)
        {
            throw new ReservationValidationException("The organizer is already a participant.");
        }

        var match = await GetPrivateMatchAsync(matchId, cancellationToken);
        RequireOrganizer(match, organizer.MemberId);
        await repository.ReplacePrivateParticipantAsync(
            matchId,
            participantId,
            organizer.MemberId,
            replacement.MemberId,
            DateTime.UtcNow,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PublicMatch>> GetPublicMatchesAsync(string matricule, CancellationToken cancellationToken)
    {
        var member = await GetActiveMemberAsync(matricule, cancellationToken);
        return await repository.GetPublicMatchesAsync(member.MemberId, DateTime.UtcNow, cancellationToken);
    }

    public async Task<PublicMatchJoinResult> JoinPublicMatchAsync(
        int matchId,
        string matricule,
        CancellationToken cancellationToken)
    {
        if (matchId <= 0)
        {
            throw new ReservationValidationException("A valid match identifier is required.");
        }

        var member = await GetActiveMemberAsync(matricule, cancellationToken);
        return await repository.JoinPublicMatchAsync(matchId, member.MemberId, DateTime.UtcNow, cancellationToken);
    }

    private async Task<ReservationMember> GetActiveMemberAsync(string matricule, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(matricule))
        {
            throw new ReservationValidationException("The matricule is required.");
        }

        var member = await repository.GetMemberAsync(matricule.Trim().ToUpperInvariant(), cancellationToken)
            ?? throw new ReservationNotFoundException("The matricule does not identify a member.");
        if (!member.IsActive)
        {
            throw new ReservationForbiddenException("Inactive members cannot participate in matches.");
        }

        return member;
    }

    private async Task<MatchDetails> GetPrivateMatchAsync(int matchId, CancellationToken cancellationToken)
    {
        if (matchId <= 0)
        {
            throw new ReservationValidationException("A valid match identifier is required.");
        }

        var match = await repository.GetMatchAsync(matchId, cancellationToken)
            ?? throw new ReservationNotFoundException("The match does not exist.");
        if (match.Visibility != ReservationVisibility.Private)
        {
            throw new ReservationForbiddenException("Participant management is available only for private matches.");
        }

        if (match.StartsAt <= DateTime.UtcNow)
        {
            throw new ReservationConflictException("Participants cannot be changed after the match has started.");
        }

        return match;
    }

    private static void RequireOrganizer(MatchDetails match, int memberId)
    {
        if (match.OrganizerMemberId != memberId)
        {
            throw new ReservationForbiddenException("Only the private match organizer can manage participants.");
        }
    }
}
