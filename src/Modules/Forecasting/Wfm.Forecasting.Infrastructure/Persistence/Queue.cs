using System.Diagnostics.CodeAnalysis;
using Wfm.Forecasting.Domain;
using Wfm.SharedKernel;

namespace Wfm.Forecasting.Infrastructure.Persistence;

// A routing destination in the CCaaS platform (domain term: Queue). Raw stats land
// per Queue; a Skill aggregates 0..n Queues into one forecast stream. Tenant-scoped
// (ADR-001). Lives in persistence for now (like Tenant); graduates to a domain
// module later (ADR-007).
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "\"Queue\" is the domain glossary term (a CCaaS routing destination), not a collection type.")]
public sealed class Queue
{
    public Queue(QueueId id, TenantId tenantId, string name)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
    }

    private Queue()
    {
    }

    public QueueId Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Name { get; private set; } = null!;
}
