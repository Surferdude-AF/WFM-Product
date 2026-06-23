using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure;
using Wfm.Forecasting.Infrastructure.Persistence;
using Wfm.SharedKernel;

namespace Wfm.IntegrationTests;

// The thin forecast pipeline end-to-end on real Postgres: ingest -> aggregate ->
// exclude outliers -> forecast (UTC) -> persist, reproducing the prototype's skill
// baseline (which excludes outlier days, unlike the 10c golden). Tenant-isolated.
public class ForecastPipelineTests
{
    private sealed class FixedTenant(TenantId? tenantId) : ITenantContext
    {
        public TenantId? TenantId { get; } = tenantId;
    }

    [DockerFact]
    public async Task Pipeline_persists_a_forecast_that_reproduces_the_prototype_skill_baseline()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var tenant = new TenantId(Guid.NewGuid());
        var skill = new SkillId(Guid.NewGuid());
        var queue = new QueueId(Guid.NewGuid());

        await SeedAndIngestAsync(postgres, tenant, skill, queue);

        await RunAsync(postgres, tenant, svc => svc.ForecastSkillAsync(skill));
        var forecast = await ReadAsync(postgres, tenant, reader => reader.ForSkillAsync(skill));

        var golden = LoadGolden();
        Assert.Equal(golden.Forecast.Count, forecast.Count);
        for (var i = 0; i < forecast.Count; i++)
        {
            Assert.Equal(golden.Forecast[i].Timestamp, forecast[i].Start.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            Assert.Equal(golden.Forecast[i].Contacts, forecast[i].Contacts);
            Assert.Equal(golden.Forecast[i].AhtSeconds, forecast[i].AhtSeconds);
        }
    }

    [DockerFact]
    public async Task A_persisted_forecast_is_tenant_isolated()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var tenantA = new TenantId(Guid.NewGuid());
        var tenantB = new TenantId(Guid.NewGuid());
        var skillA = new SkillId(Guid.NewGuid());
        var queueA = new QueueId(Guid.NewGuid());

        await SeedAndIngestAsync(postgres, tenantA, skillA, queueA);
        await RunAsync(postgres, tenantA, svc => svc.ForecastSkillAsync(skillA));

        Assert.NotEmpty(await ReadAsync(postgres, tenantA, r => r.ForSkillAsync(skillA)));
        Assert.Empty(await ReadAsync(postgres, tenantB, r => r.ForSkillAsync(skillA)));
    }

    [DockerFact]
    public async Task Pipeline_zeroes_the_forecast_outside_operating_hours()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var tenant = new TenantId(Guid.NewGuid());
        var skill = new SkillId(Guid.NewGuid());
        var queue = new QueueId(Guid.NewGuid());

        var weekday = new OpenRange(new TimeOnly(8, 0), new TimeOnly(20, 0));
        var hours = OperatingHours.ForWeek(new Dictionary<DayOfWeek, OpenRange>
        {
            [DayOfWeek.Monday] = weekday,
            [DayOfWeek.Tuesday] = weekday,
            [DayOfWeek.Wednesday] = weekday,
            [DayOfWeek.Thursday] = weekday,
            [DayOfWeek.Friday] = weekday,
        });

        var ownerOptions = new DbContextOptionsBuilder<WfmDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using (var db = new WfmDbContext(ownerOptions))
        {
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant(tenant, "Tenant"));
            db.Skills.Add(new Skill(skill, tenant, "TS", timeZoneId: null, operatingHours: hours)); // UTC
            db.Queues.Add(new Queue(queue, tenant, "support"));
            db.SkillQueues.Add(new SkillQueue(skill, queue, tenant));
            await db.SaveChangesAsync();
        }

        // Round-the-clock history: every interval would be nonzero without an operating mask.
        await using (var app = AppDbContext(postgres, tenant))
        {
            for (var day = 1; day <= 2; day++)
            {
                for (var slot = 0; slot < 96; slot++)
                {
                    var start = new DateTimeOffset(2026, 6, day, 0, 0, 0, TimeSpan.Zero).AddMinutes(15 * slot);
                    app.QueueIntervalStats.Add(new QueueIntervalStat(queue, tenant, start, 10, 300));
                }
            }

            await app.SaveChangesAsync();
        }

        await RunAsync(postgres, tenant, svc => svc.ForecastSkillAsync(skill));
        var forecast = await ReadAsync(postgres, tenant, r => r.ForSkillAsync(skill));

        Assert.NotEmpty(forecast);
        Assert.All(
            forecast.Where(p => p.Start.UtcDateTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday),
            p => Assert.Equal(0, p.Contacts));
        Assert.All(
            forecast.Where(p => p.Start.UtcDateTime.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && (p.Start.UtcDateTime.Hour < 8 || p.Start.UtcDateTime.Hour >= 20)),
            p => Assert.Equal(0, p.Contacts));
        Assert.Contains(
            forecast.Where(p => p.Start.UtcDateTime.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && p.Start.UtcDateTime.Hour >= 8 && p.Start.UtcDateTime.Hour < 20),
            p => p.Contacts > 0);
    }

    private static async Task SeedAndIngestAsync(PostgreSqlContainer postgres, TenantId tenant, SkillId skill, QueueId queue)
    {
        var ownerOptions = new DbContextOptionsBuilder<WfmDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using (var db = new WfmDbContext(ownerOptions))
        {
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant(tenant, "Tenant"));
            db.Skills.Add(new Skill(skill, tenant, "TS")); // null timezone -> UTC
            db.Queues.Add(new Queue(queue, tenant, "support"));
            db.SkillQueues.Add(new SkillQueue(skill, queue, tenant));
            await db.SaveChangesAsync();
        }

        var csv = await File.ReadAllTextAsync(FixturePath("support-history.csv"));
        await using var app = AppDbContext(postgres, tenant);
        using var reader = new StringReader(csv);
        await new CsvQueueStatsIngestion(app, new FixedTenant(tenant)).IngestAsync(queue, reader);
    }

    private static async Task RunAsync(PostgreSqlContainer postgres, TenantId tenant, Func<IForecastService, Task> run)
    {
        await using var db = AppDbContext(postgres, tenant);
        await run(new EfForecastService(db, new EfSkillIntervalStatsReader(db)));
    }

    private static async Task<IReadOnlyList<ForecastPoint>> ReadAsync(PostgreSqlContainer postgres, TenantId tenant, Func<IForecastReader, Task<IReadOnlyList<ForecastPoint>>> read)
    {
        await using var db = AppDbContext(postgres, tenant);
        return await read(new EfForecastReader(db));
    }

    private static WfmDbContext AppDbContext(PostgreSqlContainer postgres, TenantId tenant)
    {
        var appConnectionString = new NpgsqlConnectionStringBuilder(postgres.GetConnectionString())
        {
            Username = "wfm_app",
            Password = "wfm_app",
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql(appConnectionString)
            .AddInterceptors(new TenantSessionInterceptor(new FixedTenant(tenant)))
            .Options;

        return new WfmDbContext(options);
    }

    private static string FixturePath(string file) => Path.Combine(AppContext.BaseDirectory, "Fixtures", file);

    private static GoldenForecast LoadGolden()
        => JsonSerializer.Deserialize<GoldenForecast>(File.ReadAllText(FixturePath("pipeline-forecast-support.json")))!;

    private sealed record GoldenForecast(
        [property: JsonPropertyName("weekStart")] string WeekStart,
        [property: JsonPropertyName("forecast")] IReadOnlyList<GoldenRow> Forecast);

    private sealed record GoldenRow(
        [property: JsonPropertyName("timestamp")] string Timestamp,
        [property: JsonPropertyName("contacts")] int Contacts,
        [property: JsonPropertyName("aht_seconds")] int AhtSeconds);
}
