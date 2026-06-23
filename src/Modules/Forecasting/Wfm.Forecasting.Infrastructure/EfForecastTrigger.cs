using Wfm.Forecasting.Application;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure.Persistence;

namespace Wfm.Forecasting.Infrastructure;

// Enqueues a forecast job under the current tenant (RLS WITH CHECK keeps it scoped).
public sealed class EfForecastTrigger(WfmDbContext db, ITenantContext tenantContext) : IForecastTrigger
{
    private readonly TimeProvider _clock = TimeProvider.System;

    public async Task EnqueueAsync(SkillId skill, CancellationToken cancellationToken = default)
    {
        var tenant = tenantContext.TenantId
            ?? throw new InvalidOperationException("Cannot enqueue a forecast without a tenant in context.");

        db.ForecastJobs.Add(new ForecastJob(Guid.NewGuid(), skill, tenant, ForecastJob.Queued, _clock.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);
    }
}
