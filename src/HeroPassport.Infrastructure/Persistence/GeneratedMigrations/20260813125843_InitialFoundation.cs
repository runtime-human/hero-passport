using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HeroPassport.Infrastructure.Persistence.GeneratedMigrations
{
    /// <inheritdoc />
    public partial class InitialFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "heroes",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    archived_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    strain = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 20),
                    success_streak = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    total_xp = table.Column<long>(type: "INTEGER", nullable: false, defaultValue: 0L),
                    trust = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 50),
                    updated_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_heroes", x => x.id);
                    table.CheckConstraint("ck_heroes_name", "length(name) BETWEEN 1 AND 64");
                    table.CheckConstraint("ck_heroes_strain", "strain BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_heroes_success_streak", "success_streak >= 0");
                    table.CheckConstraint("ck_heroes_total_xp", "total_xp BETWEEN 0 AND 9007199254740991");
                    table.CheckConstraint("ck_heroes_trust", "trust BETWEEN 0 AND 100");
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    display_name = table.Column<string>(type: "TEXT", nullable: false),
                    identity_version = table.Column<string>(type: "TEXT", nullable: false),
                    workspace_fingerprint = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                    table.CheckConstraint("ck_projects_display_name", "length(display_name) BETWEEN 1 AND 120");
                    table.CheckConstraint("ck_projects_identity_version", "identity_version = 'project-identity/1'");
                    table.CheckConstraint("ck_projects_workspace_fingerprint", "length(workspace_fingerprint) = 64");
                });

            migrationBuilder.CreateTable(
                name: "app_settings",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false),
                    active_hero_id = table.Column<string>(type: "TEXT", nullable: true),
                    auto_finish_quest = table.Column<int>(type: "INTEGER", nullable: false),
                    auto_start_quest = table.Column<int>(type: "INTEGER", nullable: false),
                    config_version = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    locale = table.Column<string>(type: "TEXT", nullable: false),
                    presentation_style = table.Column<string>(type: "TEXT", nullable: false),
                    project_identity_salt_v1 = table.Column<byte[]>(type: "BLOB", nullable: false),
                    setup_completed = table.Column<int>(type: "INTEGER", nullable: false),
                    updated_at_utc = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_settings", x => x.id);
                    table.CheckConstraint("ck_app_settings_auto_finish", "auto_finish_quest IN (0,1)");
                    table.CheckConstraint("ck_app_settings_auto_start", "auto_start_quest IN (0,1)");
                    table.CheckConstraint("ck_app_settings_config_version", "config_version >= 1");
                    table.CheckConstraint("ck_app_settings_locale", "locale IN ('ru-RU','en-US')");
                    table.CheckConstraint("ck_app_settings_presentation_style", "presentation_style IN ('rpg_engineering','classic_rpg','minimal')");
                    table.CheckConstraint("ck_app_settings_salt", "length(project_identity_salt_v1) = 32");
                    table.CheckConstraint("ck_app_settings_setup_active_hero", "(setup_completed = 0 AND active_hero_id IS NULL) OR (setup_completed = 1 AND active_hero_id IS NOT NULL)");
                    table.CheckConstraint("ck_app_settings_setup_completed", "setup_completed IN (0,1)");
                    table.CheckConstraint("ck_app_settings_singleton", "id = 1");
                    table.ForeignKey(
                        name: "FK_app_settings_heroes_active_hero_id",
                        column: x => x.active_hero_id,
                        principalTable: "heroes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quest_sessions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    finished_at_utc = table.Column<string>(type: "TEXT", nullable: true),
                    goal = table.Column<string>(type: "TEXT", nullable: false),
                    hero_id = table.Column<string>(type: "TEXT", nullable: false),
                    locale = table.Column<string>(type: "TEXT", nullable: false),
                    project_id = table.Column<string>(type: "TEXT", nullable: false),
                    quest_type = table.Column<string>(type: "TEXT", nullable: false),
                    started_at_utc = table.Column<string>(type: "TEXT", nullable: false),
                    status = table.Column<string>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_quest_sessions", x => x.id);
                    table.CheckConstraint("ck_quest_sessions_goal", "length(goal) BETWEEN 1 AND 500");
                    table.CheckConstraint("ck_quest_sessions_locale", "locale IN ('ru-RU','en-US')");
                    table.CheckConstraint("ck_quest_sessions_status", "status IN ('open','finished')");
                    table.CheckConstraint("ck_quest_sessions_status_finished_at", "(status = 'open' AND finished_at_utc IS NULL) OR (status = 'finished' AND finished_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_quest_sessions_title", "length(title) BETWEEN 1 AND 120");
                    table.CheckConstraint("ck_quest_sessions_type", "quest_type IN ('planning','research','coding','review','debugging','documentation','maintenance')");
                    table.ForeignKey(
                        name: "FK_quest_sessions_heroes_hero_id",
                        column: x => x.hero_id,
                        principalTable: "heroes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_quest_sessions_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_app_settings_active_hero_id",
                table: "app_settings",
                column: "active_hero_id");

            migrationBuilder.CreateIndex(
                name: "ux_projects_workspace_fingerprint",
                table: "projects",
                column: "workspace_fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_quest_sessions_project_id",
                table: "quest_sessions",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ux_quest_sessions_one_open_per_hero_project",
                table: "quest_sessions",
                columns: new[] { "hero_id", "project_id" },
                unique: true,
                filter: "status = 'open'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_settings");

            migrationBuilder.DropTable(
                name: "quest_sessions");

            migrationBuilder.DropTable(
                name: "heroes");

            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}
