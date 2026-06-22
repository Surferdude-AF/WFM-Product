using Microsoft.EntityFrameworkCore;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure.Persistence;

namespace Wfm.Forecasting.Infrastructure;

public sealed class EfSkillIntervalStatsReader(WfmDbContext db) : ISkillIntervalStatsReader
{
    public async Task<IReadOnlyList<HistoricalInterval>> ForSkillAsync(SkillId skill, CancellationToken cancellationToken = default)
        => await db.SkillIntervalStats
            .Where(s => s.SkillId == skill)
            .OrderBy(s => s.IntervalStart)
            .Select(s => new HistoricalInterval(s.IntervalStart, s.Contacts, s.AhtSeconds))
            .ToListAsync(cancellationToken);
}
