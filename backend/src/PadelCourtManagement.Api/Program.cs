using PadelCourtManagement.Application;
using PadelCourtManagement.Domain;
using PadelCourtManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices();
builder.Services.AddSingleton<IAvailabilityRepository, SqlAvailabilityRepository>();
builder.Services.AddSingleton<IAdminApiRepository, SqlAdminApiRepository>();
builder.Services.AddSingleton<AdminApiService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .WithName("HealthCheck");

app.MapGroup("/api")
    .MapGet("/availability", ([AsParameters] AvailabilityRequest request, IAvailabilityService service)
        => Results.Ok(service.GetAvailability(request)));

app.MapGroup("/api")
    .MapPost("/reservations", (ReservationRequest request, IAvailabilityService service)
        => Results.Ok(service.CreateReservation(request)));

var admin = app.MapGroup("/api");

admin.MapGet("/members", (AdminApiService service) => Results.Ok(service.GetMembers()));
admin.MapPost("/members", (MemberRequest request, AdminApiService service) => Results.Ok(service.CreateMember(request)));
admin.MapPut("/members/{memberId:int}", (int memberId, MemberRequest request, AdminApiService service) => Results.Ok(service.UpdateMember(memberId, request)));
admin.MapDelete("/members/{memberId:int}", (int memberId, AdminApiService service) => { service.DeleteMember(memberId); return Results.NoContent(); });

admin.MapGet("/sites", (AdminApiService service) => Results.Ok(service.GetSites()));
admin.MapPost("/sites", (SiteRequest request, AdminApiService service) => Results.Ok(service.CreateSite(request)));
admin.MapPut("/sites/{siteId:int}", (int siteId, SiteRequest request, AdminApiService service) => Results.Ok(service.UpdateSite(siteId, request)));
admin.MapDelete("/sites/{siteId:int}", (int siteId, AdminApiService service) => { service.DeleteSite(siteId); return Results.NoContent(); });

admin.MapGet("/courts", (AdminApiService service) => Results.Ok(service.GetCourts()));
admin.MapPost("/courts", (CourtRequest request, AdminApiService service) => Results.Ok(service.CreateCourt(request)));
admin.MapPut("/courts/{courtId:int}", (int courtId, CourtRequest request, AdminApiService service) => Results.Ok(service.UpdateCourt(courtId, request)));
admin.MapDelete("/courts/{courtId:int}", (int courtId, AdminApiService service) => { service.DeleteCourt(courtId); return Results.NoContent(); });

admin.MapGet("/schedules", (AdminApiService service) => Results.Ok(service.GetSchedules()));
admin.MapPost("/schedules", (ScheduleRequest request, AdminApiService service) => Results.Ok(service.CreateSchedule(request)));
admin.MapPut("/schedules/{scheduleId:int}", (int scheduleId, ScheduleRequest request, AdminApiService service) => Results.Ok(service.UpdateSchedule(scheduleId, request)));
admin.MapDelete("/schedules/{scheduleId:int}", (int scheduleId, AdminApiService service) => { service.DeleteSchedule(scheduleId); return Results.NoContent(); });

admin.MapGet("/closures", (AdminApiService service) => Results.Ok(service.GetClosures()));
admin.MapPost("/closures", (ClosureRequest request, AdminApiService service) => Results.Ok(service.CreateClosure(request)));
admin.MapPut("/closures/{closureId:int}", (int closureId, ClosureRequest request, AdminApiService service) => Results.Ok(service.UpdateClosure(closureId, request)));
admin.MapDelete("/closures/{closureId:int}", (int closureId, AdminApiService service) => { service.DeleteClosure(closureId); return Results.NoContent(); });

app.Run();
