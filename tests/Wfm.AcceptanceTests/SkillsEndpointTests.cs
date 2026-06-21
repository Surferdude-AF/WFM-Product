using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure.Persistence;
using Wfm.SharedKernel;

namespace Wfm.AcceptanceTests;

// Story AC (scaffolding-plan step 7): GET /skills is tenant-scoped end-to-end --
// API -> Application -> Domain -> EF/Postgres -> response, with RLS active. A
// caller scoped to tenant A sees only tenant A's skills. Written first (red) to
// drive the vertical slice (double-loop TDD, ADR-006).
public sealed class SkillsEndpointTests
{
    private sealed record SkillResponse(Guid Id, string Name);

    [DockerFact]
    public async Task Get_skills_returns_only_the_requesting_tenants_skills()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());
        var skillA = new SkillId(Guid.NewGuid());
        var skillB = new SkillId(Guid.NewGuid());

        // Migrate and seed as the owner (superuser bypasses RLS) so both tenants exist.
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

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:Wfm", appConnectionString));

        var client = factory.CreateClient();
        // Temporary dev tenant seam (replaced by the real auth claim in step 8, ADR-008).
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantA.Value.ToString());

        var response = await client.GetAsync("/skills");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var skills = await response.Content.ReadFromJsonAsync<List<SkillResponse>>();

        Assert.NotNull(skills);
        var only = Assert.Single(skills);
        Assert.Equal(skillA.Value, only.Id);
        Assert.Equal("A-Billing", only.Name);
    }
}
