using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Wfm.Api;

// TEMPORARY dev authentication stub (scaffolding-plan step 8): establishes the
// caller's identity from an `Authorization: Dev <tenantId>` header so the auth
// seam is real before a managed B2B provider is wired (ADR-008 delegates authN).
// The validated principal carries the tenant it may act as; the managed
// provider's validated token (carrying the same claim) replaces this later.
public sealed class DevAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Dev";
    public const string TenantClaimType = "tenant_id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var value = header.ToString();
        const string prefix = SchemeName + " ";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(value[prefix.Length..].Trim(), out var tenantId))
        {
            return Task.FromResult(AuthenticateResult.Fail("Malformed Dev credential."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(TenantClaimType, tenantId.ToString())],
            SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
