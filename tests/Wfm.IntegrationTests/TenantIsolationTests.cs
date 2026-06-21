using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure.Persistence;
using Wfm.SharedKernel;

namespace Wfm.IntegrationTests;

// The security backstop (ADR-001): row-level security must prevent one tenant from
// ever reading another tenant's rows, even with a shared connection/role. Proven
// against real Postgres on CI.
public class TenantIsolationTests
{
    private sealed class FixedTenant(TenantId? tenantId) : ITenantContext
    {
        public TenantId? TenantId { get; } = tenantId;
    }

    [DockerFact]
    public async Task A_tenant_sees_only_its_own_skills_under_rls()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());
        var skillA = new SkillId(Guid.NewGuid());
        var skillB = new SkillId(Guid.NewGuid());

        // Migrate and seed as the owner (superuser bypasses RLS) so both tenants' rows exist.
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

        // Connect as the RLS-subject application role, scoped to tenant A.
        var appConnectionString = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Username = "wfm_app",
            Password = "wfm_app",
        }.ConnectionString;

        var appOptions = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql(appConnectionString)
            .AddInterceptors(new TenantSessionInterceptor(new FixedTenant(tenantA)))
            .Options;

        await using (var db = new WfmDbContext(appOptions))
        {
            var visible = await db.Skills.ToListAsync();

            Assert.Single(visible);
            Assert.Equal(skillA, visible[0].Id);
        }
    }

    [DockerFact]
    public async Task Writing_a_row_for_another_tenant_is_rejected()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());

        var ownerOptions = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using (var db = new WfmDbContext(ownerOptions))
        {
            await db.Database.MigrateAsync();
            db.Tenants.AddRange(new Tenant(tenantA, "Tenant A"), new Tenant(tenantB, "Tenant B"));
            await db.SaveChangesAsync();
        }

        var appConnectionString = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Username = "wfm_app",
            Password = "wfm_app",
        }.ConnectionString;

        // Scoped to tenant A, but attempts to insert a row owned by tenant B.
        var appOptions = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql(appConnectionString)
            .AddInterceptors(new TenantSessionInterceptor(new FixedTenant(tenantA)))
            .Options;

        await using var appDb = new WfmDbContext(appOptions);
        appDb.Skills.Add(new Skill(new SkillId(Guid.NewGuid()), tenantB, "B-smuggled"));

        await Assert.ThrowsAsync<DbUpdateException>(() => appDb.SaveChangesAsync());
    }
}
