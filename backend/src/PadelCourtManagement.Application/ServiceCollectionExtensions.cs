using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PadelCourtManagement.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IMatchService, MatchService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IDayBeforeService, DayBeforeService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<DayBeforeProcessingRunner>();
        services.AddOptions<DayBeforeProcessingOptions>()
            .BindConfiguration("DayBeforeProcessing")
            .Validate(
                options => options.Interval > TimeSpan.Zero,
                "Day-before processing interval must be greater than zero.")
            .ValidateOnStart();
        return services;
    }
}
