using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wfm.Forecasting.Application;

namespace Wfm.Forecasting.Infrastructure.Persistence;

// Sets the Postgres session variable `app.tenant_id` on every opened connection
// from the current tenant context, so the row-level-security policies scope every
// read and write to that tenant (ADR-001). The RLS policy treats an unset/blank
// value as "no tenant" and returns no rows -- fail-closed.
public sealed class TenantSessionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await ApplyTenantAsync(connection, cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyTenantAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
    }

    private async Task ApplyTenantAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.tenant_id', @tenant, false)";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant";
        parameter.Value = tenantContext.TenantId?.Value.ToString() ?? string.Empty;
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
