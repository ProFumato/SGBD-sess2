using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace PadelCourtManagement.Tests;

public sealed class ApiContractTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient client;

    public ApiContractTests(ApiFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_endpoint_returns_healthy_status()
    {
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", payload.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Administration_endpoint_requires_actor_header()
    {
        var response = await client.GetAsync("/api/admin/sites");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Invalid_reservation_payload_returns_bad_request()
    {
        var response = await client.PostAsJsonAsync(
            "/api/reservations",
            new
            {
                matricule = "",
                courtId = 0,
                date = "2026-08-30",
                startTime = "10:00:00",
                visibility = "Private"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Seeded_identity_endpoint_returns_member()
    {
        var response = await client.GetAsync("/api/identity/members/G0001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("G0001", payload.GetProperty("member").GetProperty("matricule").GetString());
    }

    [Fact]
    public async Task Unknown_identity_returns_not_found_problem()
    {
        var response = await client.GetAsync("/api/identity/members/G9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Unknown_administrator_returns_forbidden()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/sites");
        request.Headers.Add("X-Actor-Matricule", "G9999");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Missing_statistics_dates_return_bad_request()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/statistics");
        request.Headers.Add("X-Actor-Matricule", "G0001");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Global_statistics_endpoint_returns_report()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/admin/statistics?from=2099-01-01T00:00:00&to=2099-12-31T00:00:00");
        request.Headers.Add("X-Actor-Matricule", "G0001");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(payload.TryGetProperty("matches", out _));
        Assert.True(payload.TryGetProperty("breakdown", out _));
    }

    [Fact]
    public async Task Payment_for_unknown_match_returns_not_found()
    {
        var response = await client.PostAsync(
            "/api/matches/999999/payment?matricule=G0001",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Payment_with_unknown_outcome_returns_bad_request()
    {
        var response = await client.PostAsync(
            "/api/matches/999999/payment?matricule=G0001&outcome=NotARealOutcome",
            content: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Public_join_for_unknown_match_returns_not_found()
    {
        var response = await client.PostAsync(
            "/api/matches/999999/join?matricule=G0001",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddUserSecrets<ApiContractTests>(optional: true));
        builder.ConfigureServices(services =>
            services.RemoveAll<IHostedService>());
    }
}
