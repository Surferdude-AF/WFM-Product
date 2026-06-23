using Wfm.Forecasting.Domain;
using Wfm.SharedKernel;

namespace Wfm.Forecasting.Infrastructure.Persistence;

// A persisted forecast interval for a Skill (UTC). One row per 15-minute interval
// of the forecast week; re-forecasting replaces the Skill's rows. Tenant-scoped
// (ADR-001). `GeneratedAt` is provenance only -- the forecast values stay
// deterministic on the data.
public sealed class SkillForecast
{
    public SkillForecast(SkillId skillId, TenantId tenantId, DateTimeOffset intervalStart, int contacts, int ahtSeconds, DateTimeOffset generatedAt)
    {
        SkillId = skillId;
        TenantId = tenantId;
        IntervalStart = intervalStart;
        Contacts = contacts;
        AhtSeconds = ahtSeconds;
        GeneratedAt = generatedAt;
    }

    private SkillForecast()
    {
    }

    public SkillId SkillId { get; private set; }
    public TenantId TenantId { get; private set; }
    public DateTimeOffset IntervalStart { get; private set; }
    public int Contacts { get; private set; }
    public int AhtSeconds { get; private set; }
    public DateTimeOffset GeneratedAt { get; private set; }
}
