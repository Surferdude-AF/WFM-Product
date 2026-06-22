using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Application;

// A Skill's historical interval stream (UTC), aggregated from its mapped Queues by
// the persistence layer. The forecast core consumes this directly. Tenant scoping
// is the database's job (RLS through the aggregation view, ADR-001).
public interface ISkillIntervalStatsReader
{
    Task<IReadOnlyList<HistoricalInterval>> ForSkillAsync(SkillId skill, CancellationToken cancellationToken = default);
}
