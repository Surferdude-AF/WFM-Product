using System.Security.Claims;

namespace Wfm.Api;

// Re-binds the URL tenant to the authenticated identity on every request
// (ADR-008's "trap"): a caller may only act as the tenant their validated
// credential authorizes. A forged tenant id in the URL is denied here, with RLS
// as the backstop. DB-backed membership grants replace the claim check later.
public sealed class TenantMembershipFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var urlTenant = http.Request.RouteValues["tenantId"]?.ToString();
        var authorizedTenant = http.User.FindFirstValue(DevAuthenticationHandler.TenantClaimType);

        if (urlTenant is null
            || authorizedTenant is null
            || !string.Equals(urlTenant, authorizedTenant, StringComparison.OrdinalIgnoreCase))
        {
            return Results.Forbid();
        }

        return await next(context);
    }
}
