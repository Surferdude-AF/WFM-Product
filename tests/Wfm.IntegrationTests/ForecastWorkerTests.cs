using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure;
using Wfm.Forecasting.Infrastructure.Persistence;
using Wfm.SharedKernel;

namespace Wfm.IntegrationTests;

// The worker loop (step 11b): a queued job is claimed by the platform wfm_worker
// role, run under the job's tenant, and the forecast is persisted. The queue itself
// is tenant-isolated for the app role. Proven on real Postgres.
public class ForecastWorkerTests
{
    private sealed class FixedTenant(TenantId? tenantId) : ITenantContext
    {
        public TenantId? TenantId { get; } = tenantId;
    }

    [DockerFact]
    public async Task Worker_runs_a_queued_job_and_persists_the_forecast()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var tenant = new TenantId(Guid.NewGuid());
        var skill = new SkillId(Guid.NewGuid());
        var queue = new QueueId(Guid.NewGuid());

        var ownerOptions = new DbContextOptionsBuilder<WfmDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using (var db = new WfmDbContext(ownerOptions))
        {
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant(tenant, "Tenant"));
            db.Skills.Add(new Skill(skill, tenant, "TS"));
            db.Queues.Add(new Queue(queue, tenant, "support"));
            db.SkillQueues.Add(new SkillQueue(skill, queue, tenant));
            db.QueueIntervalStats.AddRange(
                new QueueIntervalStat(queue, tenant, new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), 10, 300),
                new QueueIntervalStat(queue, tenant, new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero), 12, 300),
                new QueueIntervalStat(queue, tenant, new DateTimeOffset(2026, 6, 3, 9, 0, 0, TimeSpan.Zero), 11, 300));
            await db.SaveChangesAsync();
        }

        await using (var app = AppDb(postgres, tenant))
        {
            await new EfForecastTrigger(app, new FixedTenant(tenant)).EnqueueAsync(skill);
        }

        Assert.True(await new ForecastJobProcessor(WorkerConnection(postgres)).ProcessNextAsync());

        await using (var app = AppDb(postgres, tenant))
        {
            Assert.NotEmpty(await new EfForecastReader(app).ForSkillAsync(skill));
        }

        await using (var db = new WfmDbContext(ownerOptions))
        {
            var status = await db.ForecastJobs.Where(j => j.SkillId == skill).Select(j => j.Status).FirstAsync();
            Assert.Equal(ForecastJob.Done, status);
        }

        // Queue drained.
        Assert.False(await new ForecastJobProcessor(WorkerConnection(postgres)).ProcessNextAsync());
    }

    [DockerFact]
    public async Task The_job_queue_is_tenant_isolated()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());
        var skillA = new SkillId(Guid.NewGuid());

        var ownerOptions = new DbContextOptionsBuilder<WfmDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using (var db = new WfmDbContext(ownerOptions))
        {
            await db.Database.MigrateAsync();
            db.Tenants.AddRange(new Tenant(tenantA, "A"), new Tenant(tenantB, "B"));
            db.Skills.Add(new Skill(skillA, tenantA, "A"));
            await db.SaveChangesAsync();
        }

        await using (var appA = AppDb(postgres, tenantA))
        {
            await new EfForecastTrigger(appA, new FixedTenant(tenantA)).EnqueueAsync(skillA);
        }

        await using (var appB = AppDb(postgres, tenantB))
        {
            Assert.Empty(await appB.ForecastJobs.ToListAsync());
        }

        await using (var appA = AppDb(postgres, tenantA))
        {
            Assert.Single(await appA.ForecastJobs.ToListAsync());
        }
    }

    private static WfmDbContext AppDb(PostgreSqlContainer postgres, TenantId tenant)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Username = "wfm_app",
            Password = "wfm_app",
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(new TenantSessionInterceptor(new FixedTenant(tenant)))
            .Options;

        return new WfmDbContext(options);
    }

    private static string WorkerConnection(PostgreSqlContainer postgres)
        => new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Username = "wfm_worker",
            Password = "wfm_worker",
        }.ConnectionString;
}
