using System.Text.Json.Serialization;
using PadelCourtManagement.Api;
using PadelCourtManagement.Application;
using PadelCourtManagement.Application.Administration;
using PadelCourtManagement.Domain;
using PadelCourtManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<AdministrationAuthorizer>();
builder.Services.AddScoped<SqlAdministrationRepository>(sp =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>()
        .GetConnectionString("PadelCourtManagement");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException(
            "Missing connection string 'PadelCourtManagement'. Configure it through user secrets or environment variables.");
    }

    return new SqlAdministrationRepository(connectionString);
});
builder.Services.AddScoped<IMemberRepository>(sp => sp.GetRequiredService<SqlAdministrationRepository>());
builder.Services.AddScoped<IAdministratorRepository>(sp => sp.GetRequiredService<SqlAdministrationRepository>());
builder.Services.AddScoped<ISiteRepository>(sp => sp.GetRequiredService<SqlAdministrationRepository>());
builder.Services.AddScoped<ICourtRepository>(sp => sp.GetRequiredService<SqlAdministrationRepository>());
builder.Services.AddScoped<IScheduleRepository>(sp => sp.GetRequiredService<SqlAdministrationRepository>());
builder.Services.AddScoped<IClosureRepository>(sp => sp.GetRequiredService<SqlAdministrationRepository>());
builder.Services.AddScoped<IAdministrationService, AdministrationService>();

builder.Services.AddApplicationServices();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<DayBeforeProcessingHostedService>();
builder.Services.AddScoped<IAvailabilityRepository, SqlAvailabilityRepository>();
builder.Services.AddScoped<IMatchRepository, SqlMatchRepository>();
builder.Services.AddScoped<IPaymentRepository, SqlPaymentRepository>();
builder.Services.AddScoped<IDayBeforeRepository, SqlDayBeforeRepository>();
builder.Services.AddScoped<IStatisticsRepository, SqlStatisticsRepository>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .WithName("HealthCheck");

var identity = app.MapGroup("/api/identity")
    .AddEndpointFilter<AdministrationExceptionFilter>();
identity.MapGet("/members/{matricule}", async (
    string matricule,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.IdentifyAsync(matricule, cancellationToken)))
    .WithName("IdentifyMember");

var administration = app.MapGroup("/api/admin")
    .AddEndpointFilter<AdministrationExceptionFilter>();

administration.MapGet("/members", async (
    HttpContext context,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetMembersAsync(context.GetActorMatricule(), cancellationToken)))
    .WithName("ListMembers");

administration.MapPost("/members", async (
    HttpContext context,
    MemberInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Created(
        $"/api/identity/members/{input.Matricule}",
        await service.CreateMemberAsync(context.GetActorMatricule(), input, cancellationToken)))
    .WithName("CreateMember");

administration.MapPut("/members/{matricule}", async (
    HttpContext context,
    string matricule,
    MemberInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.UpdateMemberAsync(
        context.GetActorMatricule(),
        matricule,
        input,
        cancellationToken)))
    .WithName("UpdateMember");

administration.MapPut("/members/{matricule}/activation", async (
    HttpContext context,
    string matricule,
    ActivationInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    {
        await service.SetMemberActivationAsync(
            context.GetActorMatricule(),
            matricule,
            input.IsActive,
            cancellationToken);
        return Results.NoContent();
    })
    .WithName("SetMemberActivation");

administration.MapPut("/members/{matricule}/administrator-role", async (
    HttpContext context,
    string matricule,
    AdministratorRoleInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    {
        await service.SetAdministratorRoleAsync(
            context.GetActorMatricule(),
            matricule,
            input,
            cancellationToken);
        return Results.NoContent();
    })
    .WithName("SetAdministratorRole");

administration.MapDelete("/members/{matricule}/administrator-role", async (
    HttpContext context,
    string matricule,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    {
        await service.RemoveAdministratorRoleAsync(
            context.GetActorMatricule(),
            matricule,
            cancellationToken);
        return Results.NoContent();
    })
    .WithName("RemoveAdministratorRole");

administration.MapGet("/sites", async (
    HttpContext context,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetSitesAsync(context.GetActorMatricule(), cancellationToken)))
    .WithName("ListSites");

administration.MapPost("/sites", async (
    HttpContext context,
    SiteInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Created(
        "/api/admin/sites",
        await service.CreateSiteAsync(context.GetActorMatricule(), input, cancellationToken)))
    .WithName("CreateSite");

administration.MapPut("/sites/{siteId:int}", async (
    HttpContext context,
    int siteId,
    SiteInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.UpdateSiteAsync(
        context.GetActorMatricule(),
        siteId,
        input,
        cancellationToken)))
    .WithName("UpdateSite");

administration.MapGet("/sites/{siteId:int}/courts", async (
    HttpContext context,
    int siteId,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetCourtsAsync(context.GetActorMatricule(), siteId, cancellationToken)))
    .WithName("ListCourts");

administration.MapPost("/sites/{siteId:int}/courts", async (
    HttpContext context,
    int siteId,
    CourtInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Created(
        $"/api/admin/sites/{siteId}/courts",
        await service.CreateCourtAsync(context.GetActorMatricule(), siteId, input, cancellationToken)))
    .WithName("CreateCourt");

administration.MapPut("/courts/{courtId:int}", async (
    HttpContext context,
    int courtId,
    CourtInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.UpdateCourtAsync(context.GetActorMatricule(), courtId, input, cancellationToken)))
    .WithName("UpdateCourt");

administration.MapGet("/sites/{siteId:int}/schedules", async (
    HttpContext context,
    int siteId,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetSchedulesAsync(context.GetActorMatricule(), siteId, cancellationToken)))
    .WithName("ListSchedules");

administration.MapPut("/sites/{siteId:int}/schedules/{calendarYear:int}", async (
    HttpContext context,
    int siteId,
    int calendarYear,
    ScheduleInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.SetScheduleAsync(
        context.GetActorMatricule(),
        siteId,
        calendarYear,
        input,
        cancellationToken)))
    .WithName("SetSchedule");

administration.MapDelete("/sites/{siteId:int}/schedules/{calendarYear:int}", async (
    HttpContext context,
    int siteId,
    int calendarYear,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    {
        await service.DeleteScheduleAsync(
            context.GetActorMatricule(),
            siteId,
            calendarYear,
            cancellationToken);
        return Results.NoContent();
    })
    .WithName("DeleteSchedule");

administration.MapGet("/closures", async (
    HttpContext context,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetClosuresAsync(context.GetActorMatricule(), cancellationToken)))
    .WithName("ListClosures");

administration.MapPost("/closures", async (
    HttpContext context,
    ClosureInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    {
        var closure = await service.CreateClosureAsync(
            context.GetActorMatricule(),
            input,
            cancellationToken);
        return Results.Created($"/api/admin/closures/{closure.ClosureId}", closure);
    })
    .WithName("CreateClosure");

administration.MapPut("/closures/{closureId:int}", async (
    HttpContext context,
    int closureId,
    ClosureInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.UpdateClosureAsync(
        context.GetActorMatricule(),
        closureId,
        input,
        cancellationToken)))
    .WithName("UpdateClosure");

administration.MapDelete("/closures/{closureId:int}", async (
    HttpContext context,
    int closureId,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    {
        await service.DeleteClosureAsync(
            context.GetActorMatricule(),
            closureId,
            cancellationToken);
        return Results.NoContent();
    })
    .WithName("DeleteClosure");

var availability = app.MapGroup("/api");
availability.MapGet("/availability", async (
    [AsParameters] AvailabilityRequest request,
    IAvailabilityService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetAvailabilityAsync(request, cancellationToken)))
    .AddEndpointFilter<AdministrationExceptionFilter>()
    .WithName("GetAvailability");
availability.MapPost("/reservations", async (
    ReservationRequest request,
    IAvailabilityService service,
    CancellationToken cancellationToken) =>
    {
        var reservation = await service.CreateReservationAsync(request, cancellationToken);
        return Results.Created($"/api/matches/{reservation.MatchId}", reservation);
    })
    .AddEndpointFilter<AdministrationExceptionFilter>()
    .WithName("CreateReservation");

var statistics = app.MapGroup("/api/admin/statistics")
    .AddEndpointFilter<AdministrationExceptionFilter>();
statistics.MapGet("/", async (
    HttpContext context,
    [AsParameters] StatisticsRequest request,
    IStatisticsService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetAsync(
       context.GetActorMatricule(),
       request,
       cancellationToken)))
    .WithName("GetStatistics");

var matches = app.MapGroup("/api/matches")
    .AddEndpointFilter<AdministrationExceptionFilter>();
matches.MapGet("/public", async (
    string matricule,
    IMatchService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetPublicMatchesAsync(matricule, cancellationToken)));
matches.MapPost("/{matchId:int}/participants", async (
    int matchId,
    PrivateParticipantInput input,
    IMatchService service,
    CancellationToken cancellationToken) =>
    {
        await service.AddPrivateParticipantAsync(matchId, input, cancellationToken);
        return Results.NoContent();
    });
matches.MapGet("/{matchId:int}/participants", async (
    int matchId,
    string matricule,
    IMatchService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetPrivateParticipantsAsync(matchId, matricule, cancellationToken)));
matches.MapDelete("/{matchId:int}/participants/{participantId:int}", async (
    int matchId,
    int participantId,
    string matricule,
    IMatchService service,
    CancellationToken cancellationToken) =>
    {
        await service.RemovePrivateParticipantAsync(matchId, participantId, matricule, cancellationToken);
        return Results.NoContent();
    });
matches.MapPut("/{matchId:int}/participants/{participantId:int}", async (
    int matchId,
    int participantId,
    PrivateParticipantInput input,
    IMatchService service,
    CancellationToken cancellationToken) =>
    {
        await service.ReplacePrivateParticipantAsync(matchId, participantId, input, cancellationToken);
        return Results.NoContent();
    });
matches.MapPost("/{matchId:int}/join", async (
    int matchId,
    string matricule,
    IMatchService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.JoinPublicMatchAsync(matchId, matricule, cancellationToken)));

matches.MapPost("/{matchId:int}/payment", async (
    int matchId,
    string matricule,
    PaymentOutcome? outcome,
    IPaymentService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.PayParticipantAsync(
        matchId,
        matricule,
        cancellationToken,
        outcome ?? PaymentOutcome.Succeeded)));

var processing = app.MapGroup("/api/processing")
    .AddEndpointFilter<AdministrationExceptionFilter>();
processing.MapPost("/day-before", async (
    HttpContext context,
    IDayBeforeService service,
    CancellationToken cancellationToken) =>
    {
        var actor = context.GetActorMatricule();
        var identityService = context.RequestServices.GetRequiredService<IAdministrationService>();
        var identity = await identityService.IdentifyAsync(actor, cancellationToken);
        if (identity.AdministratorRole is not { Scope: AdministratorScope.Global })
        {
            throw new AdministrationForbiddenException("Only a global administrator can run day-before processing.");
        }

        return Results.Ok(await service.ProcessAsync(DateTimeOffset.UtcNow, cancellationToken));
    });

app.Run();

public partial class Program;

public sealed record ActivationInput(bool IsActive);
