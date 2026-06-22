using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Infrastructure.Persistence;

// Read model over the `skill_interval_stats` view: a Skill's UTC interval stream,
// rolled up from its mapped Queues (SUM contacts, volume-weighted AHT). Keyless --
// it is projected to the domain's HistoricalInterval and fed to the forecast core.
public sealed class SkillIntervalStat
{
    public SkillId SkillId { get; private set; }
    public DateTimeOffset IntervalStart { get; private set; }
    public int Contacts { get; private set; }
    public int AhtSeconds { get; private set; }
}
