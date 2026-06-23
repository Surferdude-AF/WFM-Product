using Microsoft.EntityFrameworkCore;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure.Persistence;

namespace Wfm.Forecasting.Infrastructure;

// The thin forecast pipeline (step 11a): aggregated stats -> exclude outlier days
// -> forecast in the Skill's timezone -> persist the week. Operating hours, events,
// competition and staffing are layered on in later slices. Runs under the caller's
// tenant context, so RLS scopes both the reads and the writes (ADR-001).
public sealed class EfForecastService(WfmDbContext db, ISkillIntervalStatsReader stats) : IForecastService
{
    private readonly TimeProvider _clock = TimeProvider.System;

    public async Task ForecastSkillAsync(SkillId skill, CancellationToken cancellationToken = default)
    {
        var entity = await db.Skills.FirstOrDefaultAsync(s => s.Id == skill, cancellationToken);
        if (entity is null)
        {
            return;
        }

        var history = await stats.ForSkillAsync(skill, cancellationToken);
        if (history.Count == 0)
        {
            return;
        }

        var zone = SkillTimeZone.Of(entity.TimeZoneId);
        var clean = OutlierDetection.WithoutOutlierDays(history);

        // Forecast the week after the latest observed local day (data-derived, not clock).
        var latestLocal = DateOnly.FromDateTime(zone.ToLocal(history.Max(h => h.Start)).DateTime);
        var weekStart = NextMonday(latestLocal.AddDays(1));

        var forecast = LocalizedForecaster.Forecast(clean, zone, weekStart);

        await db.SkillForecasts.Where(f => f.SkillId == skill).ExecuteDeleteAsync(cancellationToken);
        var generatedAt = _clock.GetUtcNow();
        foreach (var point in forecast)
        {
            db.SkillForecasts.Add(new SkillForecast(skill, entity.TenantId, point.Start, point.Contacts, point.AhtSeconds, generatedAt));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static DateOnly NextMonday(DateOnly date)
    {
        while (date.DayOfWeek != DayOfWeek.Monday)
        {
            date = date.AddDays(1);
        }

        return date;
    }
}
