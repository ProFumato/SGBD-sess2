using Microsoft.Extensions.DependencyInjection;

namespace PadelCourtManagement.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IMatchService, MatchService>();
        return services;
    }
}
