using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure.Persistence;
using Wfm.SharedKernel;

namespace Wfm.AcceptanceTests;

// Story AC (scaffolding-plan step 8, ADR-008): the tenant is carried in the URL
// (`/t/{tenantId}/skills`, request-scoped) and bound to the authenticated
// identity. A dev auth stub establishes who the caller is; the URL tenant must
// match the caller's authenticated tenant before the session variable is set and
// RLS scopes the read. Written first (red) to drive the auth seam (double-loop,
// ADR-006).
public sealed class SkillsEndpointTests
{
    private sealed record SkillResponse(Guid Id, string Name);

    [DockerFact]
    public async Task An_authenticated_caller_gets_only_their_tenants_skills()
    {
        await using var db = await SeededDatabase.StartAsync();
        using var factory = db.CreateApi();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Dev", db.TenantA.Value.ToString());

        var response = await client.GetAsync($"/t/{db.TenantA.Value}/skills");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var skills = await response.Content.ReadFromJsonAsync<List<SkillResponse>>();
        Assert.NotNull(skills);
        var only = Assert.Single(skills);
        Assert.Equal(db.SkillA.Value, only.Id);
        Assert.Equal("A-Billing", only.Name);
    }

    [DockerFact]
    public async Task A_request_without_credentials_is_rejected()
    {
        await using var db = await SeededDatabase.StartAsync();
        using var factory = db.CreateApi();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/t/{db.TenantA.Value}/skills");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [DockerFact]
    public async Task A_tenant_url_that_does_not_match_the_authenticated_tenant_is_forbidden()
    {
        await using var db = await SeededDatabase.StartAsync();
        using var factory = db.CreateApi();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Dev", db.TenantA.Value.ToString());

        var response = await client.GetAsync($"/t/{db.TenantB.Value}/skills");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // The Dev stub trusts an unverified credential, so it must never let anyone in
    // outside Development -- otherwise it's an auth bypass in a hosted environment.
    // No database is needed: auth rejects the request before any data is read.
    [Fact]
    public async Task The_dev_stub_is_closed_outside_development()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.UseSetting("ConnectionStrings:Wfm", "Host=unused;Database=unused;Username=u;Password=p");
            });
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Dev", Guid.NewGuid().ToString());

        var response = await client.GetAsync($"/t/{Guid.NewGuid()}/skills");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

// A real Postgres (Testcontainers) migrated and seeded as the owner with two
// tenants' skills, plus the RLS-subject app connection string the host uses.
internal sealed class SeededDatabase : IAsyncDisposable
{
    private readonly PostgreSqlContainer _postgres;

    private SeededDatabase(
        PostgreSqlContainer postgres,
        string appConnectionString,
        TenantId tenantA,
        TenantId tenantB,
        SkillId skillA,
        SkillId skillB)
    {
        _postgres = postgres;
        AppConnectionString = appConnectionString;
        TenantA = tenantA;
        TenantB = tenantB;
        SkillA = skillA;
        SkillB = skillB;
    }

    public string AppConnectionString { get; }
    public TenantId TenantA { get; }
    public TenantId TenantB { get; }
    public SkillId SkillA { get; }
    public SkillId SkillB { get; }

    public static async Task<SeededDatabase> StartAsync()
    {
        var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());
        var skillA = new SkillId(Guid.NewGuid());
        var skillB = new SkillId(Guid.NewGuid());

        var ownerOptions = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using (var db = new WfmDbContext(ownerOptions))
        {
            await db.Database.MigrateAsync();
            db.Tenants.AddRange(new Tenant(tenantA, "Tenant A"), new Tenant(tenantB, "Tenant B"));
            db.Skills.AddRange(new Skill(skillA, tenantA, "A-Billing"), new Skill(skillB, tenantB, "B-Billing"));
            await db.SaveChangesAsync();
        }

        // The host connects as the RLS-subject application role, never the owner.
        var appConnectionString = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Username = "wfm_app",
            Password = "wfm_app",
        }.ConnectionString;

        return new SeededDatabase(postgres, appConnectionString, tenantA, tenantB, skillA, skillB);
    }

    public WebApplicationFactory<Program> CreateApi() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                // The Dev auth stub only authenticates in Development (it trusts an
                // unverified credential); pin the host there for the acceptance run.
                builder.UseEnvironment("Development");
                builder.UseSetting("ConnectionStrings:Wfm", AppConnectionString);
            });

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();
}
