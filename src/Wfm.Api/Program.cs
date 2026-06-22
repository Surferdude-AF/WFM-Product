using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Wfm.Api;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Infrastructure;
using Wfm.Forecasting.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
// The Dev stub is the only scheme today; it fails closed outside Development, so
// hosted environments reject every request until the managed provider (ADR-008)
// is wired as an additional scheme here.
builder.Services
    .AddAuthentication(DevAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(DevAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

builder.Services.AddScoped<ITenantContext, RouteTenantContext>();
builder.Services.AddScoped<TenantSessionInterceptor>();
builder.Services.AddDbContext<WfmDbContext>((sp, options) =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("Wfm"))
        .AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>()));
builder.Services.AddScoped<ISkillCatalog, EfSkillCatalog>();
builder.Services.AddSingleton<IForecastStreamReader, InMemoryForecastStreamReader>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Tenant-scoped surface: authenticated, and the URL tenant bound to the caller's
// identity before the data layer is reached (ADR-008).
var tenant = app.MapGroup("/t/{tenantId:guid}")
    .RequireAuthorization()
    .AddEndpointFilter<TenantMembershipFilter>();

tenant.MapGet("/skills", async (ISkillCatalog catalog, CancellationToken cancellationToken) =>
{
    var skills = await catalog.ListAsync(cancellationToken);
    return Results.Ok(skills.Select(s => new { id = s.Id.Value, name = s.Name }));
});

app.Run();

// Exposed so acceptance tests can spin the host via WebApplicationFactory (ADR-006).
public partial class Program { }
