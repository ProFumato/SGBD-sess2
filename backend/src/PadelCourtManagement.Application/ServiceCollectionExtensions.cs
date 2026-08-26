using Microsoft.Extensions.DependencyInjection;

namespace PadelCourtManagement.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IMatchService, MatchService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IDayBeforeService, DayBeforeService>();
        return services;
    }
}
