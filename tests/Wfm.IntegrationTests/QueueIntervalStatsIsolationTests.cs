using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Infrastructure.Persistence;
using Wfm.SharedKernel;

namespace Wfm.IntegrationTests;

// Raw per-Queue interval stats (ADR-004) are tenant-scoped (ADR-001): RLS must
// prevent one tenant from reading or writing another tenant's stats, proven on
// real Postgres.
public class QueueIntervalStatsIsolationTests
{
    private sealed class FixedTenant(TenantId? tenantId) : ITenantContext
    {
        public TenantId? TenantId { get; } = tenantId;
    }

    [DockerFact]
    public async Task A_tenant_sees_only_its_own_queue_stats_under_rls()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());
        var queueA = new QueueId(Guid.NewGuid());
        var queueB = new QueueId(Guid.NewGuid());
        var interval = new DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero);

        var ownerOptions = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using (var db = new WfmDbContext(ownerOptions))
        {
            await db.Database.MigrateAsync();
            db.Tenants.AddRange(new Tenant(tenantA, "Tenant A"), new Tenant(tenantB, "Tenant B"));
            db.Queues.AddRange(new Queue(queueA, tenantA, "support"), new Queue(queueB, tenantB, "support"));
            db.QueueIntervalStats.AddRange(
                new QueueIntervalStat(queueA, tenantA, interval, 10, 300),
                new QueueIntervalStat(queueB, tenantB, interval, 20, 300));
            await db.SaveChangesAsync();
        }

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
            var visible = await db.QueueIntervalStats.ToListAsync();

            Assert.Single(visible);
            Assert.Equal(queueA, visible[0].QueueId);
            Assert.Equal(10, visible[0].Contacts);
        }
    }

    [DockerFact]
    public async Task Writing_queue_stats_for_another_tenant_is_rejected()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());
        var queueB = new QueueId(Guid.NewGuid());
        var interval = new DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero);

        var ownerOptions = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using (var db = new WfmDbContext(ownerOptions))
        {
            await db.Database.MigrateAsync();
            db.Tenants.AddRange(new Tenant(tenantA, "Tenant A"), new Tenant(tenantB, "Tenant B"));
            db.Queues.Add(new Queue(queueB, tenantB, "support"));
            await db.SaveChangesAsync();
        }

        var appConnectionString = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Username = "wfm_app",
            Password = "wfm_app",
        }.ConnectionString;

        var appOptions = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql(appConnectionString)
            .AddInterceptors(new TenantSessionInterceptor(new FixedTenant(tenantA)))
            .Options;

        await using var appDb = new WfmDbContext(appOptions);
        appDb.QueueIntervalStats.Add(new QueueIntervalStat(queueB, tenantB, interval, 5, 300));

        await Assert.ThrowsAsync<DbUpdateException>(() => appDb.SaveChangesAsync());
    }
}
