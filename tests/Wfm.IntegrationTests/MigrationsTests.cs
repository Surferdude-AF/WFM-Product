using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure.Persistence;
using Wfm.SharedKernel;

namespace Wfm.IntegrationTests;

// Proves the forward-only migrations apply against real Postgres and that a
// tenant-scoped Skill round-trips through EF/Npgsql (ADR-002/006).
public class MigrationsTests
{
    [DockerFact]
    public async Task Migrations_apply_and_a_skill_round_trips()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        var tenantId = new TenantId(Guid.NewGuid());
        var skillId = new SkillId(Guid.NewGuid());

        await using (var db = new WfmDbContext(options))
        {
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant(tenantId, "Acme"));
            db.Skills.Add(new Skill(skillId, tenantId, "Billing"));
            await db.SaveChangesAsync();
        }

        await using (var db = new WfmDbContext(options))
        {
            var skill = await db.Skills.SingleAsync(s => s.Id == skillId);
            Assert.Equal(tenantId, skill.TenantId);
            Assert.Equal("Billing", skill.Name);
        }
    }
}
