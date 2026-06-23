using Microsoft.EntityFrameworkCore;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure;
using Wfm.Forecasting.Infrastructure.Persistence;
using Wfm.SharedKernel;

namespace Wfm.Api;

// Development-only demo seed: a fixed tenant/skill/queue loaded with one of the
// WFM-Take1 prototype contact series, so the UI has a realistic stream to forecast.
// Idempotent. Only ever wired in Development (see Program).
public static class DevSeed
{
    private static readonly TenantId DemoTenant = new(new Guid("11111111-1111-1111-1111-111111111111"));
    private static readonly SkillId DemoSkill = new(new Guid("22222222-2222-2222-2222-222222222222"));
    private static readonly QueueId DemoQueue = new(new Guid("33333333-3333-3333-3333-333333333333"));

    public static async Task<(Guid TenantId, Guid SkillId)> EnsureAsync(string connectionString)
    {
        var tenantContext = new FixedTenantContext(DemoTenant);
        var options = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(new TenantSessionInterceptor(tenantContext))
            .Options;
        await using var db = new WfmDbContext(options);

        if (!await db.Skills.AnyAsync(s => s.Id == DemoSkill))
        {
            if (!await db.Tenants.AnyAsync(t => t.Id == DemoTenant))
            {
                db.Tenants.Add(new Tenant(DemoTenant, "Demo Co"));
            }

            db.Skills.Add(new Skill(DemoSkill, DemoTenant, "Customer Service"));
            db.Queues.Add(new Queue(DemoQueue, DemoTenant, "cs"));
            db.SkillQueues.Add(new SkillQueue(DemoSkill, DemoQueue, DemoTenant));
            await db.SaveChangesAsync();

            // Load the prototype "cs" series through the real ingestion adapter.
            await using var csv = File.OpenRead(SeedSeriesPath());
            using var reader = new StreamReader(csv);
            await new CsvQueueStatsIngestion(db, tenantContext).IngestAsync(DemoQueue, reader);
        }

        return (DemoTenant.Value, DemoSkill.Value);
    }

    private static string SeedSeriesPath() => Path.Combine(AppContext.BaseDirectory, "SeedData", "historical-cs.csv");

    private sealed class FixedTenantContext(TenantId tenant) : ITenantContext
    {
        public TenantId? TenantId { get; } = tenant;
    }
}
