using Wfm.Forecasting.Application;
using Wfm.SharedKernel;

namespace Wfm.Forecasting.Infrastructure;

// The tenant the worker is currently acting as. Null while claiming a job (the
// worker sees the queue across tenants); set to the job's tenant while running its
// pipeline, so RLS scopes the reads and writes (ADR-001).
internal sealed class WorkerTenantContext(TenantId? tenantId) : ITenantContext
{
    public TenantId? TenantId { get; } = tenantId;
}
