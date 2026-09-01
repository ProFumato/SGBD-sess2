// Exception Types: Custom errors for business logic validation and authorization.
// Separate exception hierarchies for admin operations and reservations.
// Caught by exception filters to return appropriate HTTP status codes.

namespace PadelCourtManagement.Domain;

public abstract class AdministrationException(string message) : Exception(message);

public sealed class AdministrationValidationException(string message) : AdministrationException(message);

public sealed class AdministrationForbiddenException(string message) : AdministrationException(message);

public sealed class AdministrationNotFoundException(string message) : AdministrationException(message);

public sealed class AdministrationConflictException(string message) : AdministrationException(message);

public abstract class ReservationException(string message) : Exception(message);

public sealed class ReservationValidationException(string message) : ReservationException(message);

public sealed class ReservationForbiddenException(string message) : ReservationException(message);

public sealed class ReservationNotFoundException(string message) : ReservationException(message);

public sealed class ReservationConflictException(string message) : ReservationException(message);
