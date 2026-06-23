using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Wfm.Api;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Domain;
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

// Let the local Vite dev server (a different origin) call the API in Development.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
}

builder.Services.AddScoped<ITenantContext, RouteTenantContext>();
builder.Services.AddScoped<TenantSessionInterceptor>();
builder.Services.AddDbContext<WfmDbContext>((sp, options) =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("Wfm"))
        .AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>()));
builder.Services.AddScoped<ISkillCatalog, EfSkillCatalog>();
builder.Services.AddScoped<ISkillOperatingHoursStore, EfSkillOperatingHoursStore>();
builder.Services.AddScoped<ISkillIntervalStatsReader, EfSkillIntervalStatsReader>();
builder.Services.AddScoped<IForecastReader, EfForecastReader>();
builder.Services.AddScoped<IForecastTrigger, EfForecastTrigger>();
builder.Services.AddSingleton<IForecastStreamReader, InMemoryForecastStreamReader>();

// The forecast job loop (disabled unless a WfmWorker connection is configured).
builder.Services.AddHostedService<ForecastWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors();

    // Development-only demo seed for the UI: a fixed tenant/skill with sample data.
    app.MapPost("/dev/seed", async (IConfiguration configuration) =>
    {
        var (tenantId, skillId) = await DevSeed.EnsureAsync(configuration.GetConnectionString("Wfm")!);
        return Results.Ok(new { tenantId, skillId });
    });
}

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

// Configure the Skill's operating hours (ST-002 2a); the next forecast masks volume
// to these open hours. A null/omitted `weekly` resets to always open.
tenant.MapPut("/skills/{skillId:guid}/operating-hours", async (Guid skillId, OperatingHoursRequest request, ISkillOperatingHoursStore store, CancellationToken cancellationToken) =>
{
    if (!OperatingHoursRequestMapper.TryMap(request, out var hours, out var error))
    {
        return Results.BadRequest(new { error });
    }

    var found = await store.SetAsync(new SkillId(skillId), hours, cancellationToken);
    return found ? Results.NoContent() : Results.NotFound();
});

// Enqueue a forecast for the Skill; the worker runs it (step 11b).
tenant.MapPost("/skills/{skillId:guid}/forecast", async (Guid skillId, IForecastTrigger trigger, CancellationToken cancellationToken) =>
{
    await trigger.EnqueueAsync(new SkillId(skillId), cancellationToken);
    return Results.Accepted();
});

// The latest persisted forecast for the Skill, or 404 if it hasn't run yet.
tenant.MapGet("/skills/{skillId:guid}/forecast", async (Guid skillId, IForecastReader reader, CancellationToken cancellationToken) =>
{
    var forecast = await reader.ForSkillAsync(new SkillId(skillId), cancellationToken);
    return forecast.Count == 0
        ? Results.NotFound()
        : Results.Ok(forecast.Select(p => new { start = p.Start, contacts = p.Contacts, ahtSeconds = p.AhtSeconds }));
});

app.Run();

// Exposed so acceptance tests can spin the host via WebApplicationFactory (ADR-006).
public partial class Program { }
