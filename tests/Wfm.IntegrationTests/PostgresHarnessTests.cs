using Npgsql;
using Testcontainers.PostgreSql;

namespace Wfm.IntegrationTests;

// Stands up the Testcontainers + real-Postgres integration harness (ADR-006/009).
// Runs for real on CI (Docker present); skips on machines without Docker.
public class PostgresHarnessTests
{
    [DockerFact]
    public async Task Postgres_container_answers_select_1()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        await postgres.StartAsync();

        await using var connection = new NpgsqlConnection(postgres.GetConnectionString());
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("SELECT 1", connection);
        var result = (int)(await command.ExecuteScalarAsync())!;

        Assert.Equal(1, result);
    }
}
