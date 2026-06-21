using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wfm.Forecasting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnableSkillRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Runtime application role: connects subject to RLS (not the owner, not a
            // superuser, both of which bypass it). Local/test convenience only -- real
            // deployments provision this role and its secret via ops tooling (ADR-011),
            // not a committed migration.
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'wfm_app') THEN
        CREATE ROLE wfm_app LOGIN PASSWORD 'wfm_app';
    END IF;
END
$$;");

            migrationBuilder.Sql(@"GRANT USAGE ON SCHEMA public TO wfm_app;");
            migrationBuilder.Sql(@"GRANT SELECT, INSERT, UPDATE, DELETE ON tenants, skills TO wfm_app;");

            migrationBuilder.Sql(@"ALTER TABLE skills ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"ALTER TABLE skills FORCE ROW LEVEL SECURITY;");

            // Scope every row to the tenant in the `app.tenant_id` session variable.
            // An unset/blank value yields NULL, which matches no rows -- fail-closed.
            migrationBuilder.Sql(@"
CREATE POLICY skills_tenant_isolation ON skills
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP POLICY IF EXISTS skills_tenant_isolation ON skills;");
            migrationBuilder.Sql(@"ALTER TABLE skills NO FORCE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"ALTER TABLE skills DISABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql(@"REVOKE ALL ON tenants, skills FROM wfm_app;");
            migrationBuilder.Sql(@"REVOKE USAGE ON SCHEMA public FROM wfm_app;");
        }
    }
}
