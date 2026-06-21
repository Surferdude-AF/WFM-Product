using Wfm.SharedKernel;

namespace Wfm.Forecasting.Application;

// The tenant the current unit of work is scoped to. Populated from the request
// (the auth token / URL) by the host -- never trusted from the client (ADR-001/008)
// -- and consumed by infrastructure to enforce tenant scoping.
public interface ITenantContext
{
    TenantId? TenantId { get; }
}
