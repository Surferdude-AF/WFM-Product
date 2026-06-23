using Wfm.SharedKernel;

namespace Wfm.Forecasting.Domain;

// A Skill is the WFM forecasting/scheduling unit: one Skill = one forecast stream
// (maps 0..n CCaaS Queues). Tenant-scoped per ADR-001.
public sealed class Skill
{
    public Skill(SkillId id, TenantId tenantId, string name, string? timeZoneId = null, OperatingHours? operatingHours = null)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        TimeZoneId = timeZoneId;
        OperatingHours = operatingHours ?? OperatingHours.AlwaysOpen;
    }

    private Skill()
    {
    }

    public SkillId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; } = null!;

    // IANA zone the Skill forecasts in (e.g. "Europe/Berlin"); null = UTC. Resolved
    // via SkillTimeZone at forecast time (9b).
    public string? TimeZoneId { get; private set; }

    // When the operation is open, in the Skill's local time (ST-002 2a). Default is
    // always open; the pipeline zeroes the forecast outside these hours.
    public OperatingHours OperatingHours { get; private set; } = OperatingHours.AlwaysOpen;

    public void SetOperatingHours(OperatingHours operatingHours) => OperatingHours = operatingHours;
}
