using System.Diagnostics.CodeAnalysis;
using Wfm.Forecasting.Domain;
using Wfm.SharedKernel;

namespace Wfm.Forecasting.Infrastructure.Persistence;

// Maps a Queue to a Skill. Many-to-many: a Queue can feed several Skills (e.g. an
// in-house and a client-scoped view of the same queue). The Skill-aggregation view
// joins through this. Tenant-scoped (ADR-001).
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "\"Queue\" is the domain glossary term (a CCaaS routing destination), not a collection type.")]
public sealed class SkillQueue
{
    public SkillQueue(SkillId skillId, QueueId queueId, TenantId tenantId)
    {
        SkillId = skillId;
        QueueId = queueId;
        TenantId = tenantId;
    }

    private SkillQueue()
    {
    }

    public SkillId SkillId { get; private set; }
    public QueueId QueueId { get; private set; }
    public TenantId TenantId { get; private set; }
}
