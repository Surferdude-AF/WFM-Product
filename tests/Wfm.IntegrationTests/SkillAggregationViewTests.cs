using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure;
using Wfm.Forecasting.Infrastructure.Persistence;
using Wfm.SharedKernel;

namespace Wfm.IntegrationTests;

// The skill_interval_stats view rolls a Skill's mapped Queues into one UTC stream
// (SUM contacts, volume-weighted AHT == the prototype's mergeQueues) and stays
// tenant-isolated through security_invoker RLS. Proven on real Postgres.
public class SkillAggregationViewTests
{
    private sealed class FixedTenant(TenantId? tenantId) : ITenantContext
    {
        public TenantId? TenantId { get; } = tenantId;
    }

    private static readonly DateTimeOffset T0 = new(2026, 6, 8, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = new(2026, 6, 8, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 6, 8, 9, 15, 0, TimeSpan.Zero);

    [DockerFact]
    public async Task Aggregates_mapped_queues_into_one_weighted_stream()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var tenant = new TenantId(Guid.NewGuid());
        var skill = new SkillId(Guid.NewGuid());
        var q1 = new QueueId(Guid.NewGuid());
        var q2 = new QueueId(Guid.NewGuid());

        await SeedAsync(postgres, db =>
        {
            db.Tenants.Add(new Tenant(tenant, "Tenant"));
            db.Skills.Add(new Skill(skill, tenant, "CS"));
            db.Queues.AddRange(new Queue(q1, tenant, "support"), new Queue(q2, tenant, "cs"));
            db.SkillQueues.AddRange(new SkillQueue(skill, q1, tenant), new SkillQueue(skill, q2, tenant));
            db.QueueIntervalStats.AddRange(
                new QueueIntervalStat(q1, tenant, T0, 0, 350),    // zero volume -> AHT default
                new QueueIntervalStat(q1, tenant, T1, 10, 300),   // merges with q2 at T1
                new QueueIntervalStat(q2, tenant, T1, 30, 100),
                new QueueIntervalStat(q1, tenant, T2, 5, 200));   // single queue
        });

        var stream = await ReadAsync(postgres, tenant, reader => reader.ForSkillAsync(skill));

        Assert.Equal(
            new[]
            {
                new HistoricalInterval(T0, 0, 300),    // SUM 0 -> AHT 300
                new HistoricalInterval(T1, 40, 150),   // round((300*10 + 100*30)/40) = 150
                new HistoricalInterval(T2, 5, 200),
            },
            stream);
    }

    [DockerFact]
    public async Task The_view_is_tenant_isolated()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());
        var skillA = new SkillId(Guid.NewGuid());
        var skillB = new SkillId(Guid.NewGuid());
        var queueA = new QueueId(Guid.NewGuid());
        var queueB = new QueueId(Guid.NewGuid());

        await SeedAsync(postgres, db =>
        {
            db.Tenants.AddRange(new Tenant(tenantA, "A"), new Tenant(tenantB, "B"));
            db.Skills.AddRange(new Skill(skillA, tenantA, "A"), new Skill(skillB, tenantB, "B"));
            db.Queues.AddRange(new Queue(queueA, tenantA, "support"), new Queue(queueB, tenantB, "support"));
            db.SkillQueues.AddRange(new SkillQueue(skillA, queueA, tenantA), new SkillQueue(skillB, queueB, tenantB));
            db.QueueIntervalStats.AddRange(
                new QueueIntervalStat(queueA, tenantA, T1, 10, 300),
                new QueueIntervalStat(queueB, tenantB, T1, 20, 300));
        });

        // Tenant A's own skill is visible; tenant B's skill is invisible to A.
        Assert.Single(await ReadAsync(postgres, tenantA, r => r.ForSkillAsync(skillA)));
        Assert.Empty(await ReadAsync(postgres, tenantA, r => r.ForSkillAsync(skillB)));
        Assert.Empty(await ReadAsync(postgres, tenantB, r => r.ForSkillAsync(skillA)));
    }

    private static async Task SeedAsync(PostgreSqlContainer postgres, Action<WfmDbContext> seed)
    {
        var ownerOptions = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using var db = new WfmDbContext(ownerOptions);
        await db.Database.MigrateAsync();
        seed(db);
        await db.SaveChangesAsync();
    }

    private static async Task<IReadOnlyList<HistoricalInterval>> ReadAsync(
        PostgreSqlContainer postgres,
        TenantId tenant,
        Func<EfSkillIntervalStatsReader, Task<IReadOnlyList<HistoricalInterval>>> read)
    {
        var appConnectionString = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Username = "wfm_app",
            Password = "wfm_app",
        }.ConnectionString;

        var appOptions = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql(appConnectionString)
            .AddInterceptors(new TenantSessionInterceptor(new FixedTenant(tenant)))
            .Options;

        await using var db = new WfmDbContext(appOptions);
        return await read(new EfSkillIntervalStatsReader(db));
    }
}
