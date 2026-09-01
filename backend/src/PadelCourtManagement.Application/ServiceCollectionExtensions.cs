using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PadelCourtManagement.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Scoped means one service instance per HTTP request (or per scope created by the background worker).
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IMatchService, MatchService>();
        services.AddScoped<IDebtService, DebtService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IDayBeforeService, DayBeforeService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<DayBeforeProcessingRunner>();
        // Fail early if the worker interval is not usable instead of discovering it after deployment.
        services.AddOptions<DayBeforeProcessingOptions>()
            .BindConfiguration("DayBeforeProcessing")
            .Validate(
                options => options.Interval > TimeSpan.Zero,
                "Day-before processing interval must be greater than zero.")
            .ValidateOnStart();
        return services;
    }
}
