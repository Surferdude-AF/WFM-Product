using Microsoft.EntityFrameworkCore;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure.Persistence;

namespace Wfm.Forecasting.Infrastructure;

public sealed class EfForecastReader(WfmDbContext db) : IForecastReader
{
    public async Task<IReadOnlyList<ForecastPoint>> ForSkillAsync(SkillId skill, CancellationToken cancellationToken = default)
        => await db.SkillForecasts
            .Where(f => f.SkillId == skill)
            .OrderBy(f => f.IntervalStart)
            .Select(f => new ForecastPoint(f.IntervalStart, f.Contacts, f.AhtSeconds))
            .ToListAsync(cancellationToken);
}
