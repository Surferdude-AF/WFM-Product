using Wfm.SharedKernel;

namespace Wfm.Forecasting.Domain;

// A Skill is the WFM forecasting/scheduling unit: one Skill = one forecast stream
// (maps 0..n CCaaS Queues). Tenant-scoped per ADR-001.
public sealed class Skill
{
    public Skill(SkillId id, TenantId tenantId, string name)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
    }

    private Skill()
    {
    }

    public SkillId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; } = null!;
}
