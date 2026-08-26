using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Api;

public sealed class AdministrationExceptionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (AdministrationValidationException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (AdministrationForbiddenException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (AdministrationNotFoundException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (AdministrationConflictException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (ReservationValidationException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ReservationForbiddenException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ReservationNotFoundException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (ReservationConflictException exception)
        {
            return Results.Problem(exception.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }
}
