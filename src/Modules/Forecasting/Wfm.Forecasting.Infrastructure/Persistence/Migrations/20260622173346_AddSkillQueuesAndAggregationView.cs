using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wfm.Forecasting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillQueuesAndAggregationView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "skill_queues",
                columns: table => new
                {
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    queue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_queues", x => new { x.skill_id, x.queue_id });
                    table.ForeignKey(
                        name: "FK_skill_queues_queues_queue_id",
                        column: x => x.queue_id,
                        principalTable: "queues",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_skill_queues_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_skill_queues_queue_id",
                table: "skill_queues",
                column: "queue_id");

            migrationBuilder.CreateIndex(
                name: "IX_skill_queues_tenant_id",
                table: "skill_queues",
                column: "tenant_id");

            // Tenant isolation on the mapping (ADR-001), mirroring the other tables.
            migrationBuilder.Sql(@"GRANT SELECT, INSERT, UPDATE, DELETE ON skill_queues TO wfm_app;");
            migrationBuilder.Sql(@"ALTER TABLE skill_queues ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"ALTER TABLE skill_queues FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
CREATE POLICY skill_queues_tenant_isolation ON skill_queues
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);");

            // Skill-aggregation view: roll a Skill's mapped Queues up into one UTC
            // interval stream -- SUM contacts, volume-weighted AHT (300 when no
            // volume), matching the prototype's mergeQueues. security_invoker => the
            // base tables' RLS applies to the querying app role, so tenant isolation
            // holds through the view. No timezone conversion here (that's the core).
            migrationBuilder.Sql(@"
CREATE VIEW skill_interval_stats WITH (security_invoker = true) AS
SELECT sq.skill_id,
       qis.interval_start,
       SUM(qis.contacts)::int AS contacts,
       CASE WHEN SUM(qis.contacts) > 0
            THEN round(SUM(qis.aht_seconds::numeric * qis.contacts) / SUM(qis.contacts))::int
            ELSE 300 END AS aht_seconds
FROM queue_interval_stats qis
JOIN skill_queues sq ON sq.queue_id = qis.queue_id
GROUP BY sq.skill_id, qis.interval_start;");
            migrationBuilder.Sql(@"GRANT SELECT ON skill_interval_stats TO wfm_app;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP VIEW IF EXISTS skill_interval_stats;");
            migrationBuilder.Sql(@"DROP POLICY IF EXISTS skill_queues_tenant_isolation ON skill_queues;");
            migrationBuilder.Sql(@"REVOKE ALL ON skill_queues FROM wfm_app;");

            migrationBuilder.DropTable(
                name: "skill_queues");
        }
    }
}
