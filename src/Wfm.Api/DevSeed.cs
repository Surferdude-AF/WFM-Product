using Microsoft.EntityFrameworkCore;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure.Persistence;
using Wfm.SharedKernel;

namespace Wfm.Api;

// Development-only demo seed: a fixed tenant/skill/queue with a few weeks of
// synthetic, diurnally-shaped interval stats, so the UI has something to forecast.
// Idempotent. Only ever wired in Development (see Program).
public static class DevSeed
{
    private static readonly TenantId DemoTenant = new(new Guid("11111111-1111-1111-1111-111111111111"));
    private static readonly SkillId DemoSkill = new(new Guid("22222222-2222-2222-2222-222222222222"));
    private static readonly QueueId DemoQueue = new(new Guid("33333333-3333-3333-3333-333333333333"));

    public static async Task<(Guid TenantId, Guid SkillId)> EnsureAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql(connectionString)
            .AddInterceptors(new TenantSessionInterceptor(new FixedTenantContext(DemoTenant)))
            .Options;
        await using var db = new WfmDbContext(options);

        if (!await db.Tenants.AnyAsync(t => t.Id == DemoTenant))
        {
            db.Tenants.Add(new Tenant(DemoTenant, "Demo Co"));
        }

        if (!await db.Skills.AnyAsync(s => s.Id == DemoSkill))
        {
            db.Skills.Add(new Skill(DemoSkill, DemoTenant, "Customer Service"));
            db.Queues.Add(new Queue(DemoQueue, DemoTenant, "cs"));
            db.SkillQueues.Add(new SkillQueue(DemoSkill, DemoQueue, DemoTenant));
            db.QueueIntervalStats.AddRange(GenerateStats());
        }

        await db.SaveChangesAsync();
        return (DemoTenant.Value, DemoSkill.Value);
    }

    // Three weeks of 15-minute intervals with a daily bell-shaped volume curve.
    private static IEnumerable<QueueIntervalStat> GenerateStats()
    {
        var start = new DateTimeOffset(2026, 5, 18, 0, 0, 0, TimeSpan.Zero);
        for (var day = 0; day < 21; day++)
        {
            for (var i = 0; i < 96; i++)
            {
                var hour = i / 4.0;
                var shape = Math.Max(0, Math.Sin((hour - 6) / 12 * Math.PI)); // ~0 overnight, peak around noon
                var contacts = (int)Math.Round(2 + (40 * shape));
                yield return new QueueIntervalStat(DemoQueue, DemoTenant, start.AddDays(day).AddMinutes(15 * i), contacts, 300);
            }
        }
    }

    private sealed class FixedTenantContext(TenantId tenant) : ITenantContext
    {
        public TenantId? TenantId { get; } = tenant;
    }
}
