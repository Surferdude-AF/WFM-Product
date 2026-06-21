using Wfm.Forecasting.Application;
using Wfm.SharedKernel;

namespace Wfm.Api;

// TEMPORARY dev tenant seam (scaffolding-plan step 7): resolves the tenant from
// the `X-Tenant-Id` request header so the vertical slice is genuinely
// tenant-scoped before auth exists. Step 8 (ADR-008) replaces this with the
// tenant claim from the validated auth token -- the client is never trusted then.
public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public TenantId? TenantId =>
        Guid.TryParse(accessor.HttpContext?.Request.Headers["X-Tenant-Id"].ToString(), out var id)
            ? new TenantId(id)
            : null;
}
