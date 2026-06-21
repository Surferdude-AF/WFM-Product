using Microsoft.EntityFrameworkCore;
using Wfm.Api;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Infrastructure;
using Wfm.Forecasting.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<TenantSessionInterceptor>();
builder.Services.AddDbContext<WfmDbContext>((sp, options) =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("Wfm"))
        .AddInterceptors(sp.GetRequiredService<TenantSessionInterceptor>()));
builder.Services.AddScoped<ISkillCatalog, EfSkillCatalog>();
builder.Services.AddSingleton<IForecastStreamReader, InMemoryForecastStreamReader>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/skills", async (ISkillCatalog catalog, CancellationToken cancellationToken) =>
{
    var skills = await catalog.ListAsync(cancellationToken);
    return Results.Ok(skills.Select(s => new { id = s.Id.Value, name = s.Name }));
});

app.Run();

// Exposed so acceptance tests can spin the host via WebApplicationFactory (ADR-006).
public partial class Program { }
