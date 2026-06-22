using Wfm.Forecasting.Application;
using Wfm.SharedKernel;

namespace Wfm.Api;

// The active tenant is request-scoped, taken from the URL (`/t/{tenantId}/...`)
// per ADR-008 -- never a session or cookie, so concurrent tabs can't corrupt
// each other's scope. The membership filter has already confirmed the
// authenticated caller may act as this tenant before any data is read.
public sealed class RouteTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public TenantId? TenantId =>
        accessor.HttpContext?.Request.RouteValues.TryGetValue("tenantId", out var value) == true
        && Guid.TryParse(value?.ToString(), out var id)
            ? new TenantId(id)
            : null;
}
