using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure.Persistence;
using Wfm.SharedKernel;

namespace Wfm.AcceptanceTests;

// Story AC (ST-002 2a): a Skill given per-weekday operating hours forecasts zero
// outside those hours. Configured over HTTP, then exercised through the live
// pipeline end to end against real Postgres.
public sealed class OperatingHoursEndpointTests
{
    private sealed record ForecastPointDto(DateTimeOffset Start, int Contacts, int AhtSeconds);

    [DockerFact]
    public async Task Operating_hours_zero_the_forecast_outside_open_hours()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var tenant = new TenantId(Guid.NewGuid());
        var skill = new SkillId(Guid.NewGuid());
        var queue = new QueueId(Guid.NewGuid());
        await SeedAsync(postgres, db =>
        {
            db.Tenants.Add(new Tenant(tenant, "Tenant"));
            db.Skills.Add(new Skill(skill, tenant, "TS")); // UTC, always open by default
            db.Queues.Add(new Queue(queue, tenant, "support"));
            db.SkillQueues.Add(new SkillQueue(skill, queue, tenant));
            // Two weekdays of round-the-clock volume so every interval would be nonzero
            // absent an operating mask.
            for (var day = 1; day <= 2; day++)
            {
                for (var slot = 0; slot < 96; slot++)
                {
                    var start = new DateTimeOffset(2026, 6, day, 0, 0, 0, TimeSpan.Zero).AddMinutes(15 * slot);
                    db.QueueIntervalStats.Add(new QueueIntervalStat(queue, tenant, start, 10, 300));
                }
            }
        });

        using var factory = CreateApi(postgres, withWorker: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Dev", tenant.Value.ToString());

        // Configure Mon-Fri 08:00-20:00, weekends closed.
        var weekday = new { open = "08:00", close = "20:00" };
        var setHours = await client.PutAsJsonAsync(
            $"/t/{tenant.Value}/skills/{skill.Value}/operating-hours",
            new
            {
                weekly = new Dictionary<string, object>
                {
                    ["Monday"] = weekday,
                    ["Tuesday"] = weekday,
                    ["Wednesday"] = weekday,
                    ["Thursday"] = weekday,
                    ["Friday"] = weekday,
                },
            });
        Assert.Equal(HttpStatusCode.NoContent, setHours.StatusCode);

        var trigger = await client.PostAsync($"/t/{tenant.Value}/skills/{skill.Value}/forecast", content: null);
        Assert.Equal(HttpStatusCode.Accepted, trigger.StatusCode);

        var url = $"/t/{tenant.Value}/skills/{skill.Value}/forecast";
        HttpResponseMessage read = await client.GetAsync(url);
        for (var attempt = 0; attempt < 40 && read.StatusCode == HttpStatusCode.NotFound; attempt++)
        {
            await Task.Delay(500);
            read = await client.GetAsync(url);
        }

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var points = await read.Content.ReadFromJsonAsync<List<ForecastPointDto>>();
        Assert.NotNull(points);
        Assert.NotEmpty(points);

        // Weekends entirely closed; weekday intervals before 08:00 and from 20:00 closed.
        Assert.All(
            points.Where(p => p.Start.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday),
            p => Assert.Equal(0, p.Contacts));
        Assert.All(
            points.Where(p => p.Start.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && (p.Start.Hour < 8 || p.Start.Hour >= 20)),
            p => Assert.Equal(0, p.Contacts));
        // The operation is genuinely busy within open hours (mask did not zero everything).
        Assert.Contains(
            points.Where(p => p.Start.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && p.Start.Hour >= 8 && p.Start.Hour < 20),
            p => p.Contacts > 0);
    }

    private static async Task SeedAsync(PostgreSqlContainer postgres, Action<WfmDbContext> seed)
    {
        var ownerOptions = new DbContextOptionsBuilder<WfmDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db = new WfmDbContext(ownerOptions);
        await db.Database.MigrateAsync();
        seed(db);
        await db.SaveChangesAsync();
    }

    private static WebApplicationFactory<Program> CreateApi(PostgreSqlContainer postgres, bool withWorker)
        => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Wfm", AppConnection(postgres));
            if (withWorker)
            {
                builder.UseSetting("ConnectionStrings:WfmWorker", WorkerConnection(postgres));
            }
        });

    private static string AppConnection(PostgreSqlContainer postgres)
        => new NpgsqlConnectionStringBuilder(postgres.GetConnectionString()) { Username = "wfm_app", Password = "wfm_app" }.ConnectionString;

    private static string WorkerConnection(PostgreSqlContainer postgres)
        => new NpgsqlConnectionStringBuilder(postgres.GetConnectionString()) { Username = "wfm_worker", Password = "wfm_worker" }.ConnectionString;
}
