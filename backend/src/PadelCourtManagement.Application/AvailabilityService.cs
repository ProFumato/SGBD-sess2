// Availability Service: Business logic for court reservations.
// Checks court availability, validates bookings, and manages reservation visibility (public/private).

using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Application;

// Business decisions live here; the repository only supplies and persists the required data.
public sealed class AvailabilityService : IAvailabilityService
{
    private static readonly TimeZoneInfo BrusselsTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");
    private readonly IAvailabilityRepository repository;

    public AvailabilityService(IAvailabilityRepository repository)
    {
        this.repository = repository;
    }

    public async Task<IReadOnlyList<AvailableSlot>> GetAvailabilityAsync(
        AvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var member = await GetActiveMemberAsync(request.Matricule, cancellationToken);
        if (request.SiteId <= 0)
        {
            throw new ReservationValidationException("A valid site identifier is required.");
        }

        _ = member;
        return await repository.GetAvailabilityAsync(request.SiteId, request.Date, cancellationToken);
    }

    public async Task<ReservationResult> CreateReservationAsync(
        ReservationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Matricule))
        {
            throw new ReservationValidationException("The matricule is required.");
        }

        if (request.CourtId <= 0)
        {
            throw new ReservationValidationException("A valid court identifier is required.");
        }

        var startAt = request.Date.ToDateTime(request.StartTime);
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, BrusselsTimeZone).DateTime;
        if (startAt <= now)
        {
            throw new ReservationValidationException("A reservation must be in the future.");
        }

        var context = await repository.GetReservationContextAsync(
            NormalizeMatricule(request.Matricule),
            request.CourtId,
            startAt,
            now,
            cancellationToken)
            ?? throw new ReservationNotFoundException("The member or court does not exist.");

        // These checks give business errors; critical state is checked again in SQL for concurrency.
        ValidateReservation(context, startAt, now);
        return await repository.CreateReservationAsync(
            new ReservationCommand(context.Member.MemberId, context.CourtId, startAt, request.Visibility, now),
            cancellationToken);
    }

    private async Task<ReservationMember> GetActiveMemberAsync(string matricule, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(matricule))
        {
            throw new ReservationValidationException("The matricule is required.");
        }

        var member = await repository.GetMemberAsync(NormalizeMatricule(matricule), cancellationToken)
            ?? throw new ReservationNotFoundException("The matricule does not identify a member.");
        if (!member.IsActive)
        {
            throw new ReservationForbiddenException("Inactive members cannot access reservations.");
        }

        return member;
    }

    private static void ValidateReservation(ReservationContext context, DateTime startAt, DateTime now)
    {
        if (!context.Member.IsActive)
        {
            throw new ReservationForbiddenException("Inactive members cannot create reservations.");
        }

        // Booking rules depend on the member category and, for site members, their home site.
        if (context.Member.MembershipCategory == MembershipCategory.Site
            && context.Member.HomeSiteId != context.SiteId)
        {
            throw new ReservationForbiddenException("A site member can reserve only at their home site.");
        }

        var maximumDate = context.Member.MembershipCategory switch
        {
            MembershipCategory.Global => now.Date.AddDays(21),
            MembershipCategory.Site => now.Date.AddDays(14),
            MembershipCategory.Free => now.Date.AddDays(5),
            _ => throw new ArgumentOutOfRangeException(nameof(context.Member.MembershipCategory))
        };
        if (startAt.Date > maximumDate)
        {
            throw new ReservationForbiddenException("The requested date is outside this member's booking window.");
        }

        if (!context.IsCourtActive)
        {
            throw new ReservationForbiddenException("The court is inactive.");
        }

        if (context.HasActiveDebt)
        {
            throw new ReservationForbiddenException("Outstanding organizer debt blocks new reservations.");
        }

        if (context.HasActiveBookingBan)
        {
            throw new ReservationForbiddenException("An active booking ban blocks new reservations.");
        }

        // The court must be usable on that date: yearly schedule first, then closure overlap.
        if (context.OpeningTime is null || context.ClosingTime is null)
        {
            throw new ReservationConflictException("The site has no schedule for the requested year.");
        }

        if (startAt.TimeOfDay < context.OpeningTime.Value.ToTimeSpan()
            || startAt.AddMinutes(90).TimeOfDay > context.ClosingTime.Value.ToTimeSpan())
        {
            throw new ReservationConflictException("The requested match is outside the site opening hours.");
        }

        if (context.HasOverlappingClosure)
        {
            throw new ReservationConflictException("The requested match overlaps a closure.");
        }
    }

    private static string NormalizeMatricule(string matricule) => matricule.Trim().ToUpperInvariant();
}
