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

// The full persistence -> core chain (ADR-004/006): ingesting the prototype's CSV
// for a Queue, then reading the Skill-aggregation view into the forecast core,
// reproduces the prototype's skill forecast. Idempotent ingestion. Real Postgres.
public class CsvIngestionTests
{
    private sealed class FixedTenant(TenantId? tenantId) : ITenantContext
    {
        public TenantId? TenantId { get; } = tenantId;
    }

    [DockerFact]
    public async Task Ingested_csv_flows_through_the_view_to_reproduce_the_skill_forecast()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var tenant = new TenantId(Guid.NewGuid());
        var skill = new SkillId(Guid.NewGuid());
        var queue = new QueueId(Guid.NewGuid());

        var ownerOptions = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;
        await using (var db = new WfmDbContext(ownerOptions))
        {
            await db.Database.MigrateAsync();
            db.Tenants.Add(new Tenant(tenant, "Tenant"));
            db.Skills.Add(new Skill(skill, tenant, "TS"));
            db.Queues.Add(new Queue(queue, tenant, "support"));
            db.SkillQueues.Add(new SkillQueue(skill, queue, tenant));
            await db.SaveChangesAsync();
        }

        var golden = LoadGolden();
        var csv = await File.ReadAllTextAsync(FixturePath("support-history.csv"));

        // Ingest twice -> idempotent: row count equals the distinct intervals in the CSV.
        var ingested = await IngestAsync(postgres, tenant, queue, csv);
        await IngestAsync(postgres, tenant, queue, csv);
        Assert.Equal(ingested, await CountStatsAsync(ownerOptions, queue));

        var stream = await ReadStreamAsync(postgres, tenant, skill);
        var weekStart = new DateTimeOffset(
            DateTime.ParseExact(golden.WeekStart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None),
            TimeSpan.Zero);

        var forecast = BaselineForecaster.Forecast(stream, weekStart);

        Assert.Equal(golden.Forecast.Count, forecast.Count);
        for (var i = 0; i < forecast.Count; i++)
        {
            Assert.Equal(golden.Forecast[i].Timestamp, forecast[i].Start.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            Assert.Equal(golden.Forecast[i].Contacts, forecast[i].Contacts);
            Assert.Equal(golden.Forecast[i].AhtSeconds, forecast[i].AhtSeconds);
        }
    }

    private static async Task<int> IngestAsync(PostgreSqlContainer postgres, TenantId tenant, QueueId queue, string csv)
    {
        await using var db = AppDbContext(postgres, tenant);
        var ingestion = new CsvQueueStatsIngestion(db, new FixedTenant(tenant));
        using var reader = new StringReader(csv);
        return await ingestion.IngestAsync(queue, reader);
    }

    private static async Task<IReadOnlyList<HistoricalInterval>> ReadStreamAsync(PostgreSqlContainer postgres, TenantId tenant, SkillId skill)
    {
        await using var db = AppDbContext(postgres, tenant);
        return await new EfSkillIntervalStatsReader(db).ForSkillAsync(skill);
    }

    private static async Task<int> CountStatsAsync(DbContextOptions<WfmDbContext> ownerOptions, QueueId queue)
    {
        await using var db = new WfmDbContext(ownerOptions);
        return await db.QueueIntervalStats.CountAsync(s => s.QueueId == queue);
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
        => JsonSerializer.Deserialize<GoldenForecast>(File.ReadAllText(FixturePath("skill-forecast-support.json")))!;

    private sealed record GoldenForecast(
        [property: JsonPropertyName("weekStart")] string WeekStart,
        [property: JsonPropertyName("forecast")] IReadOnlyList<GoldenRow> Forecast);

    private sealed record GoldenRow(
        [property: JsonPropertyName("timestamp")] string Timestamp,
        [property: JsonPropertyName("contacts")] int Contacts,
        [property: JsonPropertyName("aht_seconds")] int AhtSeconds);
}
