using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wfm.Forecasting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillForecastsAndTimezone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "time_zone_id",
                table: "skills",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "skill_forecasts",
                columns: table => new
                {
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    interval_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contacts = table.Column<int>(type: "integer", nullable: false),
                    aht_seconds = table.Column<int>(type: "integer", nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_forecasts", x => new { x.skill_id, x.interval_start });
                    table.ForeignKey(
                        name: "FK_skill_forecasts_skills_skill_id",
                        column: x => x.skill_id,
                        principalTable: "skills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_skill_forecasts_tenant_id",
                table: "skill_forecasts",
                column: "tenant_id");

            // Tenant isolation (ADR-001), mirroring the skills policy.
            migrationBuilder.Sql(@"GRANT SELECT, INSERT, UPDATE, DELETE ON skill_forecasts TO wfm_app;");
            migrationBuilder.Sql(@"ALTER TABLE skill_forecasts ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"ALTER TABLE skill_forecasts FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"
CREATE POLICY skill_forecasts_tenant_isolation ON skill_forecasts
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP POLICY IF EXISTS skill_forecasts_tenant_isolation ON skill_forecasts;");
            migrationBuilder.Sql(@"REVOKE ALL ON skill_forecasts FROM wfm_app;");

            migrationBuilder.DropTable(
                name: "skill_forecasts");

            migrationBuilder.DropColumn(
                name: "time_zone_id",
                table: "skills");
        }
    }
}
