// HTTP Context Extensions: Helper method to extract admin actor matricule from request header.
// Used to identify who is performing administrative actions.

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
            throw new InvalidOperationException("Administration requests require exactly one X-Actor-Matricule header.");
        }

        return values[0]!;
    }
}
