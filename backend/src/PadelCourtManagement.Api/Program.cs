using System.Text.Json.Serialization;
using PadelCourtManagement.Api;
using PadelCourtManagement.Application.Administration;
using PadelCourtManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<AdministrationAuthorizer>();
builder.Services.AddScoped<SqlAdministrationRepository>();
builder.Services.AddScoped<IMemberRepository>(sp => sp.GetRequiredService<SqlAdministrationRepository>());
builder.Services.AddScoped<IAdministratorRepository>(sp => sp.GetRequiredService<SqlAdministrationRepository>());
builder.Services.AddScoped<ISiteRepository>(sp => sp.GetRequiredService<SqlAdministrationRepository>());
builder.Services.AddScoped<ICourtRepository>(sp => sp.GetRequiredService<SqlAdministrationRepository>());
builder.Services.AddScoped<IScheduleRepository>(sp => sp.GetRequiredService<SqlAdministrationRepository>());
builder.Services.AddScoped<IClosureRepository>(sp => sp.GetRequiredService<SqlAdministrationRepository>());
builder.Services.AddScoped<IAdministrationService, AdministrationService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .WithName("HealthCheck");

app.MapGet("/api/identity/members/{matricule}", async (
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

app.Run();

public partial class Program;

public sealed record ActivationInput(bool IsActive);
