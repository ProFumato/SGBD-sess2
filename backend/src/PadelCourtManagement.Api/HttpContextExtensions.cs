using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Api;

public static class HttpContextExtensions
{
    public static string GetActorMatricule(this HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue("X-Actor-Matricule", out var values)
            || values.Count != 1
            || string.IsNullOrWhiteSpace(values[0]))
        {
            throw new AdministrationValidationException("Administration requests require exactly one X-Actor-Matricule header.");
        }

        return values[0]!;
    }
}
