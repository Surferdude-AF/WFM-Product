using Wfm.Forecasting.Application;
using Wfm.Forecasting.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IForecastStreamReader, InMemoryForecastStreamReader>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

// Exposed so acceptance tests can spin the host via WebApplicationFactory (ADR-006).
public partial class Program { }
