using Wfm.SharedKernel;

namespace Wfm.Forecasting.Infrastructure.Persistence;

// One observed 15-minute interval of raw stats for a Queue, stored as a UTC instant
// (ADR-004). The Skill-aggregation view rolls these up across a Skill's Queues; the
// Skill's timezone is applied later, in the domain core (9b) -- the store stays UTC.
public sealed class QueueIntervalStat
{
    public QueueIntervalStat(QueueId queueId, TenantId tenantId, DateTimeOffset intervalStart, int contacts, int ahtSeconds)
    {
        QueueId = queueId;
        TenantId = tenantId;
        IntervalStart = intervalStart;
        Contacts = contacts;
        AhtSeconds = ahtSeconds;
    }

    private QueueIntervalStat()
    {
    }

    public QueueId QueueId { get; private set; }
    public TenantId TenantId { get; private set; }
    public DateTimeOffset IntervalStart { get; private set; }
    public int Contacts { get; private set; }
    public int AhtSeconds { get; private set; }
}
