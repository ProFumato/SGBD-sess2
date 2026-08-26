using System.Text.Json.Serialization;
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

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .WithName("HealthCheck");

app.MapGet("/api/identity/members/{matricule}", async (
    string matricule,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.IdentifyAsync(matricule, cancellationToken)))
    .WithName("IdentifyMember");

var admin = app.MapGroup("/api/admin");

admin.MapGet("/members", async (
    HttpContext context,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetMembersAsync(context.GetActorMatricule(), cancellationToken)))
    .WithName("ListMembers");

admin.MapPost("/members", async (
    HttpContext context,
    MemberInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Created("/api/admin/members", await service.CreateMemberAsync(context.GetActorMatricule(), input, cancellationToken)))
    .WithName("CreateMember");

admin.MapPut("/members/{matricule}", async (
    HttpContext context,
    string matricule,
    MemberInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.UpdateMemberAsync(context.GetActorMatricule(), matricule, input, cancellationToken)))
    .WithName("UpdateMember");

admin.MapPut("/members/{matricule}/activation", async (
    HttpContext context,
    string matricule,
    ActivationInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    {
        await service.SetMemberActivationAsync(context.GetActorMatricule(), matricule, input.IsActive, cancellationToken);
        return Results.NoContent();
    })
    .WithName("SetMemberActivation");

admin.MapPut("/members/{matricule}/administrator-role", async (
    HttpContext context,
    string matricule,
    AdministratorRoleInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    {
        await service.SetAdministratorRoleAsync(context.GetActorMatricule(), matricule, input, cancellationToken);
        return Results.NoContent();
    })
    .WithName("SetAdministratorRole");

admin.MapDelete("/members/{matricule}/administrator-role", async (
    HttpContext context,
    string matricule,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    {
        await service.RemoveAdministratorRoleAsync(context.GetActorMatricule(), matricule, cancellationToken);
        return Results.NoContent();
    })
    .WithName("RemoveAdministratorRole");

admin.MapGet("/sites", async (
    HttpContext context,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetSitesAsync(context.GetActorMatricule(), cancellationToken)))
    .WithName("ListSites");

admin.MapPost("/sites", async (
    HttpContext context,
    SiteInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Created("/api/admin/sites", await service.CreateSiteAsync(context.GetActorMatricule(), input, cancellationToken)))
    .WithName("CreateSite");

admin.MapPut("/sites/{siteId:int}", async (
    HttpContext context,
    int siteId,
    SiteInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.UpdateSiteAsync(context.GetActorMatricule(), siteId, input, cancellationToken)))
    .WithName("UpdateSite");

admin.MapGet("/sites/{siteId:int}/courts", async (
    HttpContext context,
    int siteId,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetCourtsAsync(context.GetActorMatricule(), siteId, cancellationToken)))
    .WithName("ListCourts");

admin.MapPost("/sites/{siteId:int}/courts", async (
    HttpContext context,
    int siteId,
    CourtInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Created($"/api/admin/sites/{siteId}/courts", await service.CreateCourtAsync(context.GetActorMatricule(), siteId, input, cancellationToken)))
    .WithName("CreateCourt");

admin.MapPut("/courts/{courtId:int}", async (
    HttpContext context,
    int courtId,
    CourtInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.UpdateCourtAsync(context.GetActorMatricule(), courtId, input, cancellationToken)))
    .WithName("UpdateCourt");

admin.MapGet("/sites/{siteId:int}/schedules", async (
    HttpContext context,
    int siteId,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetSchedulesAsync(context.GetActorMatricule(), siteId, cancellationToken)))
    .WithName("ListSchedules");

admin.MapPut("/sites/{siteId:int}/schedules/{calendarYear:int}", async (
    HttpContext context,
    int siteId,
    int calendarYear,
    ScheduleInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.SetScheduleAsync(context.GetActorMatricule(), siteId, calendarYear, input, cancellationToken)))
    .WithName("SetSchedule");

admin.MapDelete("/sites/{siteId:int}/schedules/{calendarYear:int}", async (
    HttpContext context,
    int siteId,
    int calendarYear,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    {
        await service.DeleteScheduleAsync(context.GetActorMatricule(), siteId, calendarYear, cancellationToken);
        return Results.NoContent();
    })
    .WithName("DeleteSchedule");

admin.MapGet("/closures", async (
    HttpContext context,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetClosuresAsync(context.GetActorMatricule(), cancellationToken)))
    .WithName("ListClosures");

admin.MapPost("/closures", async (
    HttpContext context,
    ClosureInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Created("/api/admin/closures", await service.CreateClosureAsync(context.GetActorMatricule(), input, cancellationToken)))
    .WithName("CreateClosure");

admin.MapPut("/closures/{closureId:int}", async (
    HttpContext context,
    int closureId,
    ClosureInput input,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.UpdateClosureAsync(context.GetActorMatricule(), closureId, input, cancellationToken)))
    .WithName("UpdateClosure");

admin.MapDelete("/closures/{closureId:int}", async (
    HttpContext context,
    int closureId,
    IAdministrationService service,
    CancellationToken cancellationToken) =>
    {
        await service.DeleteClosureAsync(context.GetActorMatricule(), closureId, cancellationToken);
        return Results.NoContent();
    })
    .WithName("DeleteClosure");

app.Run();

public partial class Program;

public sealed record ActivationInput(bool IsActive);
