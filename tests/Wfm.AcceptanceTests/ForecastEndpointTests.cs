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

// Story AC (step 11b): triggering a forecast over HTTP enqueues a job, the worker
// runs it, and the result becomes readable for the authenticated tenant. Driven end
// to end through the host (with the worker running) against real Postgres.
public sealed class ForecastEndpointTests
{
    private sealed record ForecastPointDto(DateTimeOffset Start, int Contacts, int AhtSeconds);

    [DockerFact]
    public async Task Triggering_a_forecast_makes_it_readable()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        var tenant = new TenantId(Guid.NewGuid());
        var skill = new SkillId(Guid.NewGuid());
        var queue = new QueueId(Guid.NewGuid());
        await SeedAsync(postgres, db =>
        {
            db.Tenants.Add(new Tenant(tenant, "Tenant"));
            db.Skills.Add(new Skill(skill, tenant, "TS"));
            db.Queues.Add(new Queue(queue, tenant, "support"));
            db.SkillQueues.Add(new SkillQueue(skill, queue, tenant));
            db.QueueIntervalStats.AddRange(
                new QueueIntervalStat(queue, tenant, new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero), 10, 300),
                new QueueIntervalStat(queue, tenant, new DateTimeOffset(2026, 6, 2, 9, 0, 0, TimeSpan.Zero), 12, 300));
        });

        using var factory = CreateApi(postgres, withWorker: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Dev", tenant.Value.ToString());

        var trigger = await client.PostAsync($"/t/{tenant.Value}/skills/{skill.Value}/forecast", content: null);
        Assert.Equal(HttpStatusCode.Accepted, trigger.StatusCode);

        // The worker processes asynchronously; poll until the forecast is readable.
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
    }

    [DockerFact]
    public async Task A_forecast_for_another_tenant_is_forbidden()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();
        await SeedAsync(postgres, _ => { }); // schema only

        using var factory = CreateApi(postgres, withWorker: false);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Dev", Guid.NewGuid().ToString());

        var response = await client.GetAsync($"/t/{Guid.NewGuid()}/skills/{Guid.NewGuid()}/forecast");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
