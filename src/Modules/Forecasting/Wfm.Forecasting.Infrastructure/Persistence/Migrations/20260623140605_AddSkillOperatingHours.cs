using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wfm.Forecasting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillOperatingHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The JSON null literal is always-open (OperatingHoursJson), so existing
            // Skills keep 24/7 behaviour with no backfill.
            migrationBuilder.AddColumn<string>(
                name: "operating_hours",
                table: "skills",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'null'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "operating_hours",
                table: "skills");
        }
    }
}
