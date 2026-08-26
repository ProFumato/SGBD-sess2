using PadelCourtManagement.Application;
using PadelCourtManagement.Domain;
using PadelCourtManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddSingleton<IAvailabilityRepository, SqlAvailabilityRepository>();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .WithName("HealthCheck");

app.MapGroup("/api")
    .MapGet("/availability", ([AsParameters] AvailabilityRequest request, IAvailabilityService service)
        => Results.Ok(service.GetAvailability(request)));

app.MapGroup("/api")
    .MapPost("/reservations", (ReservationRequest request, IAvailabilityService service)
        => Results.Ok(service.CreateReservation(request)));

app.Run();
