using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wfm.Forecasting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQueueIntervalStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "queues",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_queues", x => x.id);
                    table.ForeignKey(
                        name: "FK_queues_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "queue_interval_stats",
                columns: table => new
                {
                    queue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    interval_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contacts = table.Column<int>(type: "integer", nullable: false),
                    aht_seconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_queue_interval_stats", x => new { x.queue_id, x.interval_start });
                    table.ForeignKey(
                        name: "FK_queue_interval_stats_queues_queue_id",
                        column: x => x.queue_id,
                        principalTable: "queues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_queue_interval_stats_tenant_id",
                table: "queue_interval_stats",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_queues_tenant_id",
                table: "queues",
                column: "tenant_id");

            // Tenant isolation (ADR-001): the runtime app role is subject to RLS;
            // every row is scoped to the tenant in the `app.tenant_id` session
            // variable, fail-closed when unset. Mirrors the skills policy.
            migrationBuilder.Sql(@"GRANT SELECT, INSERT, UPDATE, DELETE ON queues, queue_interval_stats TO wfm_app;");

            migrationBuilder.Sql(@"ALTER TABLE queues ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"ALTER TABLE queues FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
CREATE POLICY queues_tenant_isolation ON queues
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);");

            migrationBuilder.Sql(@"ALTER TABLE queue_interval_stats ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"ALTER TABLE queue_interval_stats FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
CREATE POLICY queue_interval_stats_tenant_isolation ON queue_interval_stats
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP POLICY IF EXISTS queue_interval_stats_tenant_isolation ON queue_interval_stats;");
            migrationBuilder.Sql(@"DROP POLICY IF EXISTS queues_tenant_isolation ON queues;");
            migrationBuilder.Sql(@"REVOKE ALL ON queues, queue_interval_stats FROM wfm_app;");

            migrationBuilder.DropTable(
                name: "queue_interval_stats");

            migrationBuilder.DropTable(
                name: "queues");
        }
    }
}
