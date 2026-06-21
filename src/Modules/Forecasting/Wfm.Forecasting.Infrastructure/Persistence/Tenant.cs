using Wfm.SharedKernel;

namespace Wfm.Forecasting.Infrastructure.Persistence;

// Platform tenant backing the `tenants` table and the tenant_id foreign key.
// Tenancy is cross-cutting; this lives here for the walking skeleton and will
// graduate to a dedicated IAM/Platform module (ADR-007) when that lands.
public sealed class Tenant
{
    public Tenant(TenantId id, string name)
    {
        Id = id;
        Name = name;
    }

    private Tenant()
    {
    }

    public TenantId Id { get; private set; }
    public string Name { get; private set; } = null!;
}
