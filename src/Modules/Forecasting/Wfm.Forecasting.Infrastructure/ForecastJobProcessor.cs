using Microsoft.EntityFrameworkCore;
using Npgsql;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure.Persistence;
using Wfm.SharedKernel;

namespace Wfm.Forecasting.Infrastructure;

// Claims and runs one forecast job (step 11b). Connects as the platform `wfm_worker`
// role, which can see the queue across tenants; once a job is claimed it runs the
// pipeline under that job's tenant (the session variable is set per the job), so
// the stat reads and forecast writes stay RLS-scoped. Claiming uses
// FOR UPDATE SKIP LOCKED so multiple workers never grab the same job.
public sealed class ForecastJobProcessor(string workerConnectionString)
{
    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        await using var control = CreateContext(tenant: null);
        await control.Database.OpenConnectionAsync(cancellationToken);

        var claimed = await ClaimAsync(control, cancellationToken);
        if (claimed is null)
        {
            return false;
        }

        var (jobId, skill, tenant) = claimed.Value;
        try
        {
            await using var scoped = CreateContext(tenant);
            await new EfForecastService(scoped, new EfSkillIntervalStatsReader(scoped)).ForecastSkillAsync(skill, cancellationToken);
            await MarkAsync(control, jobId, ForecastJob.Done, cancellationToken);
        }
        catch
        {
            await MarkAsync(control, jobId, ForecastJob.Failed, cancellationToken);
            throw;
        }

        return true;
    }

    private WfmDbContext CreateContext(TenantId? tenant)
    {
        var options = new DbContextOptionsBuilder<WfmDbContext>()
            .UseNpgsql(workerConnectionString)
            .AddInterceptors(new TenantSessionInterceptor(new WorkerTenantContext(tenant)))
            .Options;
        return new WfmDbContext(options);
    }

    private static async Task<(Guid Id, SkillId Skill, TenantId Tenant)?> ClaimAsync(WfmDbContext control, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)control.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = @"
UPDATE forecast_jobs SET status = 'running'
WHERE id = (
    SELECT id FROM forecast_jobs
    WHERE status = 'queued'
    ORDER BY requested_at
    FOR UPDATE SKIP LOCKED
    LIMIT 1)
RETURNING id, skill_id, tenant_id;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return (reader.GetGuid(0), new SkillId(reader.GetGuid(1)), new TenantId(reader.GetGuid(2)));
    }

    private static async Task MarkAsync(WfmDbContext control, Guid jobId, string status, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)control.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE forecast_jobs SET status = @status, completed_at = now() WHERE id = @id;";
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("id", jobId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
