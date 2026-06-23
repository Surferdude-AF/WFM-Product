using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wfm.Forecasting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddForecastJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "forecast_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_forecast_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_forecast_jobs_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_forecast_jobs_skill_id",
                table: "forecast_jobs",
                column: "skill_id");

            migrationBuilder.CreateIndex(
                name: "IX_forecast_jobs_status",
                table: "forecast_jobs",
                column: "status");

            // Platform worker role: processes the queue across tenants. Local/test
            // convenience like wfm_app -- real deployments provision it via ops
            // tooling (ADR-011), not a committed migration.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'wfm_worker') THEN
        CREATE ROLE wfm_worker LOGIN PASSWORD 'wfm_worker';
    END IF;
END
$$;");
            migrationBuilder.Sql(@"GRANT USAGE ON SCHEMA public TO wfm_worker;");

            // The trigger (wfm_app) sees/enqueues only its own tenant's jobs; the
            // worker (wfm_worker) sees and updates every tenant's jobs so it can
            // claim across the queue, then sets app.tenant_id per job for the run.
            migrationBuilder.Sql(@"GRANT SELECT, INSERT, UPDATE, DELETE ON forecast_jobs TO wfm_app;");
            migrationBuilder.Sql(@"GRANT SELECT, UPDATE ON forecast_jobs TO wfm_worker;");
            migrationBuilder.Sql(@"ALTER TABLE forecast_jobs ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"ALTER TABLE forecast_jobs FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
CREATE POLICY forecast_jobs_tenant_isolation ON forecast_jobs
    USING (current_user = 'wfm_worker' OR tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (current_user = 'wfm_worker' OR tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);");

            // The worker runs the pipeline scoped by app.tenant_id, so it needs the
            // same tenant tables (subject to their existing per-tenant policies).
            migrationBuilder.Sql(@"GRANT SELECT ON skills, queue_interval_stats, skill_queues, skill_interval_stats TO wfm_worker;");
            migrationBuilder.Sql(@"GRANT SELECT, INSERT, UPDATE, DELETE ON skill_forecasts TO wfm_worker;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP POLICY IF EXISTS forecast_jobs_tenant_isolation ON forecast_jobs;");
            migrationBuilder.Sql(@"REVOKE ALL ON skill_forecasts FROM wfm_worker;");
            migrationBuilder.Sql(@"REVOKE ALL ON skills, queue_interval_stats, skill_queues, skill_interval_stats FROM wfm_worker;");
            migrationBuilder.Sql(@"REVOKE ALL ON forecast_jobs FROM wfm_app, wfm_worker;");

            migrationBuilder.DropTable(
                name: "forecast_jobs");
        }
    }
}
